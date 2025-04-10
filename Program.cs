
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.Text.Json.Serialization;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using SednaRag.Services;
using SednaRag.Middleware;
using SednaRag.Helpers;
using FluentValidation.AspNetCore;
using FluentValidation;
using Microsoft.Win32;

namespace SednaRag
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            // Aggiungi servizi al container
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

            // Configurazione Azure OpenAI
            builder.Services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var endpoint = new Uri(config["Azure:OpenAI:Endpoint"]);
                var key = config["Azure:OpenAI:Key"];
                return new AzureOpenAIClient(endpoint, new AzureKeyCredential(key));
            });

            // Configurazione Azure Cognitive Search
            builder.Services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var endpoint = new Uri(config["Azure:Search:Endpoint"]);
                var key = config["Azure:Search:Key"];
                var indexName = config["Azure:Search:IndexName"];
                return new SearchClient(endpoint, indexName, new AzureKeyCredential(key));
            });

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddMemoryCache(); // o un'altra implementazione di IDistributedCache


            // Registra l'HttpClient per il servizio di licenze
            builder.Services.AddHttpClient("LicenseService", (sp,client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                client.BaseAddress = new Uri(config["LicenseService:BaseUrl"]);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            // Servizi per RAG
            builder.Services.AddSingleton<RagService>();
            builder.Services.AddSingleton<SchemaImportService>();

            // Cache distribuita per ottimizzare richieste ripetute
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration.GetConnectionString("Redis");
                options.InstanceName = "OrtofruttaRAG_";
            });

            // Aggiungi rate limiting
            builder.Services.AddRateLimiting(builder.Configuration);

            // Aggiungi validazione richieste
            builder.Services.AddRequestValidation();

            // Aggiungi middleware per logging e telemetria
            builder.Services.AddApplicationInsightsTelemetry();

            // Configura CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins",
                    builder => builder
                        .WithOrigins(
                            "https://app.ortofruttaerp.com",
                            "https://client.ortofruttaerp.com")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                );
            });

            // Aggiungi Swagger per documentazione API
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ERP Ortofrutticolo RAG API",
                    Version = "v1",
                    Description = "API per il sistema RAG dell'ERP Ortofrutticolo"
                });
            });


            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseRateLimiting();
            app.UseHttpsRedirection();
            app.UseCors("AllowSpecificOrigins");

            // Middleware personalizzato per contesto cliente
            app.UseMiddleware<ClientContextMiddleware>();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }


 }
