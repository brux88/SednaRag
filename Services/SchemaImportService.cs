using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Embeddings;
using SednaRag.Models;
using System.Text;
using System.Text.Json;

namespace SednaRag.Services
{
    public class SchemaImportService
    {
        private readonly AzureOpenAIClient _openAIClient;
        private readonly SearchClient _searchClient;
        private readonly ILogger<SchemaImportService> _logger;
        private readonly IConfiguration _configuration;

        public SchemaImportService(
            AzureOpenAIClient openAIClient,
            SearchClient searchClient,
            IConfiguration configuration,
            ILogger<SchemaImportService> logger)
        {
            _openAIClient = openAIClient;
            _searchClient = searchClient;
            _configuration = configuration;
            _logger = logger;
        }

        // Importa i dati dello schema forniti manualmente
        public async Task<SchemaImportResult> ImportSchemaAsync(
            string clientId,
            List<TableDefinition> tables,
            List<StoredProcedureDefinition> storedProcedures,
            List<BusinessRuleDefinition> businessRules,
            List<QueryExample> queryExamples,
            Dictionary<string, object> additionalMetadata)
        {
            _logger.LogInformation("Inizio importazione schema per client {ClientId}", clientId);

            var result = new SchemaImportResult
            {
                DocumentsCreated = 0,
                DocumentsUpdated = 0
            };

            var schemaDocuments = new List<SchemaDocument>();

            // Processa tabelle
            if (tables != null && tables.Count > 0)
            {
                foreach (var table in tables)
                {
                    // Genera documento per la tabella
                    var tableDoc = new SchemaDocument
                    {
                        Id = $"{clientId}_table_{table.Name}",
                        ClientId = clientId,
                        Title = $"Tabella: {table.Name}",
                        Content = GenerateTableContent(table),
                        ContentType = "schema_table",
                        Module = table.Module ?? "all",
                        Keywords = GenerateKeywordsForTable(table)
                    };

                    schemaDocuments.Add(tableDoc);
                }

                _logger.LogInformation("Processate {Count} tabelle", tables.Count);
            }

            // Processa stored procedures
            if (storedProcedures != null && storedProcedures.Count > 0)
            {
                foreach (var sp in storedProcedures)
                {
                    // Genera documento per la stored procedure
                    var spDoc = new SchemaDocument
                    {
                        Id = $"{clientId}_sp_{sp.Name}",
                        ClientId = clientId,
                        Title = $"Stored Procedure: {sp.Name}",
                        Content = GenerateStoredProcedureContent(sp),
                        ContentType = "schema_stored_procedure",
                        Module = sp.Module ?? "all",
                        Keywords = GenerateKeywordsForSP(sp)
                    };

                    schemaDocuments.Add(spDoc);
                }

                _logger.LogInformation("Processate {Count} stored procedure", storedProcedures.Count);
            }

            // Processa regole di business
            if (businessRules != null && businessRules.Count > 0)
            {
                foreach (var rule in businessRules)
                {
                    // Genera documento per la regola di business
                    var ruleDoc = new SchemaDocument
                    {
                        Id = $"{clientId}_rule_{Guid.NewGuid()}",
                        ClientId = clientId,
                        Title = $"Regola: {rule.Name}",
                        Content = GenerateBusinessRuleContent(rule),
                        ContentType = "business_rule",
                        Module = rule.Module ?? "all",
                        Keywords = rule.Keywords ?? new List<string>()
                    };

                    schemaDocuments.Add(ruleDoc);
                }

                _logger.LogInformation("Processate {Count} regole di business", businessRules.Count);
            }

            // Processa esempi di query
            if (queryExamples != null && queryExamples.Count > 0)
            {
                foreach (var example in queryExamples)
                {
                    // Genera documento per l'esempio di query
                    var exampleDoc = new SchemaDocument
                    {
                        Id = $"{clientId}_example_{Guid.NewGuid()}",
                        ClientId = clientId,
                        Title = $"Esempio Query: {example.Description}",
                        Content = GenerateQueryExampleContent(example),
                        ContentType = "query_example",
                        Module = example.Module ?? "all",
                        Keywords = example.Keywords ?? new List<string>()
                    };

                    schemaDocuments.Add(exampleDoc);
                }

                _logger.LogInformation("Processati {Count} esempi di query", queryExamples.Count);
            }

            // Genera embeddings per tutti i documenti
            await GenerateEmbeddingsAsync(schemaDocuments);

            // Carica nell'indice di Azure Cognitive Search
            result = await UploadToSearchIndexAsync(schemaDocuments);

            return result;
        }

        // Aggiungi un singolo esempio di query
        public async Task<bool> AddQueryExampleAsync(string clientId, QueryExample example)
        {
            var exampleDoc = new SchemaDocument
            {
                Id = $"{clientId}_example_{Guid.NewGuid()}",
                ClientId = clientId,
                Title = $"Esempio Query: {example.Description}",
                Content = GenerateQueryExampleContent(example),
                ContentType = "query_example",
                Module = example.Module ?? "all",
                Keywords = example.Keywords ?? new List<string>()
            };

            // Genera embedding
            await GenerateEmbeddingsAsync(new List<SchemaDocument> { exampleDoc });

            // Carica nell'indice
            var response = await _searchClient.MergeOrUploadDocumentsAsync(new List<SchemaDocument> { exampleDoc });

            return response.Value.Results[0].Succeeded;
        }

        private string GenerateTableContent(TableDefinition table)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Nome tabella: {table.Name}");
            sb.AppendLine($"Descrizione: {table.Description}");
            sb.AppendLine();

            // Aggiungi informazioni sui campi
            sb.AppendLine("Campi:");
            if (table.Columns != null && table.Columns.Count > 0)
            {
                foreach (var column in table.Columns)
                {
                    sb.AppendLine($"- {column.Name} ({column.DataType})" +
                                 (column.IsPrimaryKey ? " [PK]" : "") +
                                 (column.IsForeignKey ? $" [FK -> {column.ReferencedTable}.{column.ReferencedColumn}]" : "") +
                                 $": {column.Description}");
                }
            }

            // Aggiungi informazioni sulle relazioni
            if (table.Relations != null && table.Relations.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Relazioni:");
                foreach (var relation in table.Relations)
                {
                    sb.AppendLine($"- {relation.Type}: {relation.FromColumn} -> {relation.ToTable}.{relation.ToColumn}");
                }
            }

            // Aggiungi informazioni sugli indici
            if (table.Indexes != null && table.Indexes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Indici:");
                foreach (var index in table.Indexes)
                {
                    sb.AppendLine($"- {index.Name}: {string.Join(", ", index.Columns)} [{(index.IsUnique ? "UNIQUE" : "NON-UNIQUE")}]");
                }
            }

            // Aggiungi informazioni sull'utilizzo comune
            if (!string.IsNullOrEmpty(table.CommonUsage))
            {
                sb.AppendLine();
                sb.AppendLine("Utilizzo comune:");
                sb.AppendLine(table.CommonUsage);
            }

            return sb.ToString();
        }

        private string GenerateStoredProcedureContent(StoredProcedureDefinition sp)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Nome procedure: {sp.Name}");
            sb.AppendLine($"Descrizione: {sp.Description}");
            sb.AppendLine();

            // Aggiungi informazioni sui parametri
            if (sp.Parameters != null && sp.Parameters.Count > 0)
            {
                sb.AppendLine("Parametri:");
                foreach (var param in sp.Parameters)
                {
                    sb.AppendLine($"- @{param.Name} ({param.DataType})" +
                                 (param.IsOutput ? " [OUTPUT]" : "") +
                                 $": {param.Description}");
                }
            }

            // Aggiungi informazioni sul risultato
            if (!string.IsNullOrEmpty(sp.ResultDescription))
            {
                sb.AppendLine();
                sb.AppendLine("Risultato:");
                sb.AppendLine(sp.ResultDescription);
            }

            // Aggiungi informazioni sull'utilizzo
            if (!string.IsNullOrEmpty(sp.Usage))
            {
                sb.AppendLine();
                sb.AppendLine("Utilizzo:");
                sb.AppendLine(sp.Usage);
            }

            return sb.ToString();
        }

        private string GenerateBusinessRuleContent(BusinessRuleDefinition rule)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Nome regola: {rule.Name}");
            sb.AppendLine($"Descrizione: {rule.Description}");
            sb.AppendLine();

            // Aggiungi dettagli della regola
            sb.AppendLine("Dettagli:");
            sb.AppendLine(rule.Details);

            // Aggiungi tabelle correlate
            if (rule.RelatedTables != null && rule.RelatedTables.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Tabelle correlate:");
                foreach (var table in rule.RelatedTables)
                {
                    sb.AppendLine($"- {table}");
                }
            }

            // Aggiungi esempi
            if (!string.IsNullOrEmpty(rule.Examples))
            {
                sb.AppendLine();
                sb.AppendLine("Esempi:");
                sb.AppendLine(rule.Examples);
            }

            return sb.ToString();
        }

        private string GenerateQueryExampleContent(QueryExample example)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Descrizione: {example.Description}");
            sb.AppendLine();

            // Aggiungi SQL della query
            sb.AppendLine("SQL:");
            sb.AppendLine(example.SqlQuery);

            // Aggiungi spiegazione
            if (!string.IsNullOrEmpty(example.Explanation))
            {
                sb.AppendLine();
                sb.AppendLine("Spiegazione:");
                sb.AppendLine(example.Explanation);
            }

            // Aggiungi casi d'uso
            if (!string.IsNullOrEmpty(example.UseCase))
            {
                sb.AppendLine();
                sb.AppendLine("Casi d'uso:");
                sb.AppendLine(example.UseCase);
            }

            return sb.ToString();
        }

        private List<string> GenerateKeywordsForTable(TableDefinition table)
        {
            var keywords = new List<string>();

            // Aggiungi nome tabella
            keywords.Add(table.Name);

            // Aggiungi nomi di colonne chiave
            if (table.Columns != null)
            {
                foreach (var column in table.Columns.Where(c => c.IsPrimaryKey || c.IsForeignKey))
                {
                    keywords.Add(column.Name);
                }
            }

            // Aggiungi keywords esplicite
            if (table.Keywords != null)
            {
                keywords.AddRange(table.Keywords);
            }

            return keywords.Distinct().ToList();
        }

        private List<string> GenerateKeywordsForSP(StoredProcedureDefinition sp)
        {
            var keywords = new List<string>();

            // Aggiungi nome della procedura
            keywords.Add(sp.Name);

            // Aggiungi tokens dal nome (separando con underscore o camelCase)
            keywords.AddRange(sp.Name.Split('_'));

            // Usa regex per separare camelCase
            var parts = System.Text.RegularExpressions.Regex.Split(sp.Name, @"(?<!^)(?=[A-Z])");
            keywords.AddRange(parts);

            // Aggiungi keywords esplicite
            if (sp.Keywords != null)
            {
                keywords.AddRange(sp.Keywords);
            }

            return keywords.Distinct().ToList();
        }

        private async Task GenerateEmbeddingsAsync(List<SchemaDocument> documents)
        {
            var embeddingModelName = _configuration["Azure:OpenAI:EmbeddingModel"] ?? "text-embedding-ada-002";

            foreach (var document in documents)
            {
                try
                {
                    // Combina titolo e contenuto per l'embedding
                    string textToEmbed = document.Title + "\n\n" + document.Content;

                    // Genera embedding tramite Azure OpenAI
                   /* var embeddingOptions = new EmbeddingsOptions(textToEmbed)
                    {
                        DeploymentName = embeddingModelName
                    };*/
                    var embeddingClient = _openAIClient.GetEmbeddingClient(embeddingModelName);
                    var response = await embeddingClient.GenerateEmbeddingAsync( textToEmbed );


                    var embedding = response.Value.ToFloats();


                    if (!embedding.IsEmpty)
                    {
                        document.ContentVector = embedding.ToArray();
                    }
                    else
                    {
                        // Gestisci il caso in cui l'embedding non sia stato generato
                        throw new InvalidOperationException($"Embedding non generato per il documento con titolo: {document.Title}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore nella generazione embedding per documento {Id}", document.Id);
                    throw;
                }
            }
        }

        private async Task<SchemaImportResult> UploadToSearchIndexAsync(List<SchemaDocument> documents)
        {
            var result = new SchemaImportResult
            {
                DocumentsCreated = 0,
                DocumentsUpdated = 0
            };

            // Suddividi in batch di max 1000 documenti (limite di Azure Cognitive Search)
            const int batchSize = 1000;
            //documents = new List<SchemaDocument> { documents.FirstOrDefault() };
            for (int i = 0; i < documents.Count; i += batchSize)
            {
                var batch = documents.Skip(i).Take(batchSize).ToList();
             

                // Crea batch di operazioni
                var indexActions = new List<IndexDocumentsAction<SchemaDocument>>();
                foreach (var doc in batch)
                {
                    var jsonDoc = JsonSerializer.Serialize(doc);
                    _logger.LogInformation("Documento JSON: {Json}", jsonDoc);
                    indexActions.Add(IndexDocumentsAction.MergeOrUpload(doc));
                }

                try
                {
                    // Esegui upload batch
                    IndexDocumentsBatch<SchemaDocument> indexBatch = IndexDocumentsBatch.Create(indexActions.ToArray());
                    var response = await _searchClient.IndexDocumentsAsync(indexBatch);

                    // Conteggio risultati
                    foreach (var resultItem in response.Value.Results)
                    {
                        if (resultItem.Succeeded)
                        {
                            // Verifica se è una creazione o aggiornamento (semplificato)
                            if (resultItem.Status == 201)
                                result.DocumentsCreated++;
                            else
                                result.DocumentsUpdated++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore nell'upload batch di documenti a Cognitive Search");
                    throw;
                }
            }

            return result;
        }


        public async Task<ClientSchema> GetFullSchemaAsync(string clientId)
        {
            try
            {
                _logger.LogInformation("Recupero dello schema completo per client {ClientId}", clientId);

                // Prepara la ricerca per recuperare tutti i documenti per il client
                var searchOptions = new SearchOptions
                {
                    Size = 1000, // Recupera fino a 1000 documenti (il limite massimo)
                    Select = { "Id", "Title", "Content", "ContentType", "Module", "Keywords" },
                    Filter = $"ClientId eq '{clientId}'"
                };

                // Esegui la ricerca
                var searchResponse = await _searchClient.SearchAsync<SchemaDocument>("*", searchOptions);
                var results =   searchResponse.Value.GetResults();

                _logger.LogInformation($"Recuperati   documenti dello schema" );

                // Organizza i risultati per ricreare lo schema completo
                var clientSchema = new ClientSchema
                {
                    ClientId = clientId,
                    Tables = new List<TableDefinition>(),
                    StoredProcedures = new List<StoredProcedureDefinition>(),
                    BusinessRules = new List<BusinessRuleDefinition>(),
                    QueryExamples = new List<QueryExample>(),
                    AdditionalMetadata = new Dictionary<string, object>()
                };

                // Mappa i documenti recuperati nelle rispettive strutture dello schema
                foreach (var result in results)
                {
                    switch (result.Document.ContentType)
                    {
                        case "schema_table":
                            var table = ParseTableDefinition(result.Document);
                            if (table != null)
                                clientSchema.Tables.Add(table);
                            break;

                        case "schema_stored_procedure":
                            var sp = ParseStoredProcedureDefinition(result.Document);
                            if (sp != null)
                                clientSchema.StoredProcedures.Add(sp);
                            break;

                        case "business_rule":
                            var rule = ParseBusinessRuleDefinition(result.Document);
                            if (rule != null)
                                clientSchema.BusinessRules.Add(rule);
                            break;

                        case "query_example":
                            var example = ParseQueryExampleDefinition(result.Document);
                            if (example != null)
                                clientSchema.QueryExamples.Add(example);
                            break;
                    }
                }

                return clientSchema;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel recupero dello schema completo per il client {ClientId}", clientId);
                throw;
            }
        }

        // Metodi di parsing per convertire i documenti nelle strutture originali dello schema

        private TableDefinition ParseTableDefinition(SchemaDocument document)
        {
            try
            {
                // Estrai il nome della tabella dal titolo
                // Formato tipico: "Tabella: NomeTabella"
                string tableName = document.Title.Replace("Tabella: ", "").Trim();

                // Crea un oggetto TableDefinition di base
                var table = new TableDefinition
                {
                    Name = tableName,
                    Module = document.Module,
                    Keywords = document.Keywords?.ToList() ?? new List<string>()
                };

                // Estrai la descrizione e altri dettagli dal contenuto
                var lines = document.Content.Split('\n');

                // Parsing dello stato attuale del documento per estrarre le informazioni
                // Questo è un esempio semplificato che andrà adattato al formato esatto dei tuoi documenti
                foreach (var line in lines)
                {
                    if (line.StartsWith("Descrizione: "))
                        table.Description = line.Replace("Descrizione: ", "").Trim();

                    // Qui andrebbe aggiunta la logica per estrarre colonne, relazioni, indici, ecc.
                    // basata sul formato specifico in cui hai salvato queste informazioni
                }

                // Nota: questo metodo deve essere espanso per recuperare tutti i dettagli completi,
                // inclusi colonne, relazioni, indici, ecc. dal testo formattato

                return table;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel parsing della definizione della tabella per il documento {Id}", document.Id);
                return null;
            }
        }

        private StoredProcedureDefinition ParseStoredProcedureDefinition(SchemaDocument document)
        {
            try
            {
                // Estrai il nome della stored procedure dal titolo
                // Formato tipico: "Stored Procedure: NomeProcedura"
                string spName = document.Title.Replace("Stored Procedure: ", "").Trim();

                // Crea un oggetto StoredProcedureDefinition di base
                var sp = new StoredProcedureDefinition
                {
                    Name = spName,
                    Module = document.Module,
                    Keywords = document.Keywords?.ToList() ?? new List<string>()
                };

                // Parsing del contenuto
                var lines = document.Content.Split('\n');

                foreach (var line in lines)
                {
                    if (line.StartsWith("Descrizione: "))
                        sp.Description = line.Replace("Descrizione: ", "").Trim();

                    // Qui va aggiunta la logica per estrarre parametri, descrizione del risultato, ecc.
                }

                return sp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel parsing della definizione della stored procedure per il documento {Id}", document.Id);
                return null;
            }
        }

        private BusinessRuleDefinition ParseBusinessRuleDefinition(SchemaDocument document)
        {
            try
            {
                // Estrai il nome della regola dal titolo
                // Formato tipico: "Regola: NomeRegola"
                string ruleName = document.Title.Replace("Regola: ", "").Trim();

                // Crea un oggetto BusinessRuleDefinition di base
                var rule = new BusinessRuleDefinition
                {
                    Name = ruleName,
                    Module = document.Module,
                    Keywords = document.Keywords?.ToList() ?? new List<string>()
                };

                // Parsing del contenuto
                var lines = document.Content.Split('\n');

                foreach (var line in lines)
                {
                    if (line.StartsWith("Descrizione: "))
                        rule.Description = line.Replace("Descrizione: ", "").Trim();

                    // Qui va aggiunta la logica per estrarre dettagli, esempi, tabelle correlate, ecc.
                }

                return rule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel parsing della definizione della regola di business per il documento {Id}", document.Id);
                return null;
            }
        }

        private QueryExample ParseQueryExampleDefinition(SchemaDocument document)
        {
            try
            {
                // Crea un oggetto QueryExample di base
                var example = new QueryExample
                {
                    Description = document.Title.Replace("Esempio Query: ", "").Trim(),
                    Module = document.Module,
                    Keywords = document.Keywords?.ToList() ?? new List<string>()
                };

                // Parsing del contenuto
                var content = document.Content;

                // Estrai SQL query
                int sqlStart = content.IndexOf("SQL:");
                int explanationStart = content.IndexOf("Spiegazione:");

                if (sqlStart >= 0 && explanationStart > sqlStart)
                {
                    example.SqlQuery = content.Substring(sqlStart + 4, explanationStart - sqlStart - 4).Trim();
                }

                // Estrai spiegazione
                int useCaseStart = content.IndexOf("Casi d'uso:");

                if (explanationStart >= 0 && useCaseStart > explanationStart)
                {
                    example.Explanation = content.Substring(explanationStart + 12, useCaseStart - explanationStart - 12).Trim();
                }

                // Estrai caso d'uso
                if (useCaseStart >= 0)
                {
                    example.UseCase = content.Substring(useCaseStart + 11).Trim();
                }

                return example;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel parsing dell'esempio di query per il documento {Id}", document.Id);
                return null;
            }
        }
    }
}
