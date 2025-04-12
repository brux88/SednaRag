using System.Reflection;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using SednaRag.Models;

namespace SednaRag.Services
{
    public class ErpOperatorService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ErpOperatorService> _logger;
        private readonly IDistributedCache _cache;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ErpActionService _actionService;

        public ErpOperatorService(
            IConfiguration configuration,
            ILogger<ErpOperatorService> logger,
            IDistributedCache cache,
            IHttpContextAccessor httpContextAccessor,
            ErpActionService actionService)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
            _httpContextAccessor = httpContextAccessor;
            _actionService = actionService;
        }

        // Esegue un'azione ERP
        public async Task<ErpActionResponse> ExecuteActionAsync(
            string actionName,
            string clientId,
            Dictionary<string, object> parameters = null)
        {
            try
            {
                _logger.LogInformation("Esecuzione azione ERP '{Action}' per client {ClientId}", actionName, clientId);

                // 1. Recupera la definizione dell'azione dall'archivio
                var actionDefinition = await _actionService.GetActionByNameAsync(clientId, actionName);
                if (actionDefinition == null)
                {
                    return new ErpActionResponse
                    {
                        Success = false,
                        Error = $"Azione '{actionName}' non trovata"
                    };
                }

                // 2. Verifica che l'utente abbia i permessi necessari
                if (!await HasPermissionForActionAsync(clientId, actionDefinition))
                {
                    return new ErpActionResponse
                    {
                        Success = false,
                        Error = "Permessi insufficienti per eseguire questa azione"
                    };
                }

                // 3. Valida i parametri
                var validationResult = ValidateParameters(actionDefinition, parameters);
                if (!validationResult.isValid)
                {
                    return new ErpActionResponse
                    {
                        Success = false,
                        Error = validationResult.errorMessage
                    };
                }

                // 4. Esegui l'azione dinamicamente
                var executionResult = await ExecuteDynamicActionAsync(actionDefinition, parameters);

                return new ErpActionResponse
                {
                    Success = true,
                    Result = executionResult,
                    Message = $"Azione '{actionName}' eseguita con successo"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'esecuzione dell'azione ERP '{Action}' per client {ClientId}", actionName, clientId);

                return new ErpActionResponse
                {
                    Success = false,
                    Error = $"Errore nell'esecuzione dell'azione: {ex.Message}"
                };
            }
        }

        // Verifica se l'utente corrente ha i permessi per eseguire l'azione
        private async Task<bool> HasPermissionForActionAsync(string clientId, ErpActionDefinition actionDefinition)
        {
            // Qui implementeresti la logica per verificare i permessi dell'utente
            // Per ora restituiamo sempre true come esempio
            return true;
        }

        // Valida i parametri forniti contro quelli richiesti dall'azione
        private (bool isValid, string errorMessage) ValidateParameters(
            ErpActionDefinition actionDefinition,
            Dictionary<string, object> parameters)
        {
            if (parameters == null)
            {
                parameters = new Dictionary<string, object>();
            }

            // Verifica che tutti i parametri richiesti siano presenti
            foreach (var paramDef in actionDefinition.Parameters.Where(p => p.Required))
            {
                if (!parameters.ContainsKey(paramDef.Name))
                {
                    return (false, $"Parametro richiesto mancante: {paramDef.Name}");
                }
            }

            // Verifica che i tipi di dati siano compatibili
            foreach (var param in parameters)
            {
                var paramDef = actionDefinition.Parameters.FirstOrDefault(p => p.Name == param.Key);
                if (paramDef == null)
                {
                    return (false, $"Parametro non riconosciuto: {param.Key}");
                }

                // Implementa qui una validazione più approfondita dei tipi
                // Per ora facciamo una validazione semplice
                if (!IsCompatibleType(param.Value, paramDef.DataType))
                {
                    return (false, $"Tipo incompatibile per parametro {param.Key}: atteso {paramDef.DataType}, ricevuto {param.Value?.GetType().Name ?? "null"}");
                }
            }

            return (true, null);
        }

        // Verifica se il valore è compatibile con il tipo di dati atteso
        private bool IsCompatibleType(object value, string expectedType)
        {
            if (value == null)
            {
                // Null è compatibile solo con tipi reference
                return expectedType != "int" && expectedType != "double" && expectedType != "decimal" &&
                       expectedType != "bool" && expectedType != "datetime";
            }

            var actualType = value.GetType().Name.ToLowerInvariant();
            expectedType = expectedType.ToLowerInvariant();

            // Gestisci le corrispondenze di tipo comuni
            switch (expectedType)
            {
                case "string":
                    return value is string;
                case "int":
                case "int32":
                    return value is int || value is long || (value is string && int.TryParse((string)value, out _));
                case "long":
                case "int64":
                    return value is long || value is int || (value is string && long.TryParse((string)value, out _));
                case "double":
                case "float":
                    return value is double || value is float || value is int ||
                           (value is string && double.TryParse((string)value, out _));
                case "decimal":
                    return value is decimal || value is double || value is float || value is int ||
                           (value is string && decimal.TryParse((string)value, out _));
                case "bool":
                case "boolean":
                    return value is bool || (value is string && bool.TryParse((string)value, out _));
                case "datetime":
                case "date":
                    return value is DateTime || (value is string && DateTime.TryParse((string)value, out _));
                case "object":
                case "any":
                    return true; // Qualsiasi tipo è compatibile
                default:
                    // Per tipi complessi, dovremmo implementare una validazione più sofisticata
                    return true;
            }
        }

        // Esegue l'azione dinamicamente utilizzando reflection
        private async Task<object> ExecuteDynamicActionAsync(
            ErpActionDefinition actionDefinition,
            Dictionary<string, object> parameters)
        {
            try
            {
                // Ottieni la connessione al client ERP
                var erpConnection = GetErpConnection(actionDefinition.ClientId);

                // Prepara i parametri convertiti nei tipi corretti
                var convertedParams = ConvertParameters(actionDefinition.Parameters, parameters);

                // Carica l'assembly dinamicamente
                var assembly = Assembly.Load(actionDefinition.AssemblyName);
                if (assembly == null)
                {
                    throw new InvalidOperationException($"Assembly non trovato: {actionDefinition.AssemblyName}");
                }

                // Trova il tipo che contiene il metodo
                var typeName = ExtractTypeNameFromFunction(actionDefinition.FunctionName);
                var type = assembly.GetType(typeName);
                if (type == null)
                {
                    throw new InvalidOperationException($"Tipo non trovato: {typeName}");
                }

                // Crea un'istanza della classe e inizializzala con la connessione ERP
                var instance = Activator.CreateInstance(type, erpConnection);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Impossibile creare un'istanza di: {typeName}");
                }

                // Trova il metodo da invocare
                var methodName = ExtractMethodNameFromFunction(actionDefinition.FunctionName);
                var method = type.GetMethod(methodName);
                if (method == null)
                {
                    throw new InvalidOperationException($"Metodo non trovato: {methodName}");
                }

                // Prepara i parametri del metodo
                var methodParams = method.GetParameters();
                var paramValues = new object[methodParams.Length];

                for (int i = 0; i < methodParams.Length; i++)
                {
                    var paramName = methodParams[i].Name;
                    if (convertedParams.ContainsKey(paramName))
                    {
                        paramValues[i] = convertedParams[paramName];
                    }
                    else if (methodParams[i].HasDefaultValue)
                    {
                        paramValues[i] = methodParams[i].DefaultValue;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Parametro mancante: {paramName}");
                    }
                }

                // Invoca il metodo
                var result = method.Invoke(instance, paramValues);

                // Se è un Task, attendi che sia completato
                if (result is Task task)
                {
                    await task;

                    // Se è un Task<T>, estrai il risultato
                    var resultProperty = task.GetType().GetProperty("Result");
                    if (resultProperty != null)
                    {
                        result = resultProperty.GetValue(task);
                    }
                    else
                    {
                        result = null; // Task senza risultato
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nell'esecuzione dinamica dell'azione ERP");
                throw new InvalidOperationException($"Errore nell'esecuzione dell'azione: {ex.Message}", ex);
            }
        }

        // Ottiene la connessione al client ERP
        private object GetErpConnection(string clientId)
        {
            // Qui implementeresti la logica per ottenere una connessione al sistema ERP
            // Per ora restituiamo un oggetto vuoto come esempio
            return new object();
        }

        // Converte i parametri nei tipi corretti
        private Dictionary<string, object> ConvertParameters(
            List<ErpActionParameter> paramDefinitions,
            Dictionary<string, object> parameters)
        {
            var result = new Dictionary<string, object>();

            foreach (var param in parameters)
            {
                var paramDef = paramDefinitions.FirstOrDefault(p => p.Name == param.Key);
                if (paramDef == null)
                {
                    // Ignora parametri non riconosciuti
                    continue;
                }

                try
                {
                    var convertedValue = ConvertValue(param.Value, paramDef.DataType);
                    result[param.Key] = convertedValue;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Errore nella conversione del parametro {param.Key}: {ex.Message}");
                }
            }

            // Aggiungi valori predefiniti per parametri mancanti
            foreach (var paramDef in paramDefinitions)
            {
                if (!result.ContainsKey(paramDef.Name) && paramDef.DefaultValue != null)
                {
                    result[paramDef.Name] = paramDef.DefaultValue;
                }
            }

            return result;
        }

        // Converte un valore nel tipo specificato
        private object ConvertValue(object value, string targetType)
        {
            if (value == null)
            {
                return null;
            }

            targetType = targetType.ToLowerInvariant();

            switch (targetType)
            {
                case "string":
                    return value.ToString();
                case "int":
                case "int32":
                    if (value is string strVal)
                        return int.Parse(strVal);
                    return Convert.ToInt32(value);
                case "long":
                case "int64":
                    if (value is string strVal2)
                        return long.Parse(strVal2);
                    return Convert.ToInt64(value);
                case "double":
                    if (value is string strVal3)
                        return double.Parse(strVal3);
                    return Convert.ToDouble(value);
                case "decimal":
                    if (value is string strVal4)
                        return decimal.Parse(strVal4);
                    return Convert.ToDecimal(value);
                case "bool":
                case "boolean":
                    if (value is string strVal5)
                        return bool.Parse(strVal5);
                    return Convert.ToBoolean(value);
                case "datetime":
                case "date":
                    if (value is string strVal6)
                        return DateTime.Parse(strVal6);
                    return Convert.ToDateTime(value);
                default:
                    // Per tipi complessi, potrebbe essere necessario implementare una logica di conversione più avanzata
                    return value;
            }
        }

        // Estrae il nome del tipo dal nome completo della funzione
        private string ExtractTypeNameFromFunction(string functionName)
        {
            int lastDotIndex = functionName.LastIndexOf('.');
            if (lastDotIndex == -1)
            {
                throw new InvalidOperationException($"Nome funzione non valido: {functionName}. Formato atteso: Namespace.Tipo.Metodo");
            }

            return functionName.Substring(0, lastDotIndex);
        }

        // Estrae il nome del metodo dal nome completo della funzione
        private string ExtractMethodNameFromFunction(string functionName)
        {
            int lastDotIndex = functionName.LastIndexOf('.');
            if (lastDotIndex == -1)
            {
                throw new InvalidOperationException($"Nome funzione non valido: {functionName}. Formato atteso: Namespace.Tipo.Metodo");
            }

            return functionName.Substring(lastDotIndex + 1);
        }
    }
}