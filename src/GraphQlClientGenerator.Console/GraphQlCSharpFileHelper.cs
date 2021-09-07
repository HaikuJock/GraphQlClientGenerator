﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.IO;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace GraphQlClientGenerator.Console
{
    internal static class GraphQlCSharpFileHelper
    {
        public static async Task<int> GenerateGraphQlClientSourceCode(IConsole console, ProgramOptions options)
        {
            try
            {
                var generatedFiles = new List<FileInfo>();
                await GenerateClientSourceCode(options, generatedFiles);

                foreach (var file in generatedFiles)
                    console.Out.WriteLine($"File {file.FullName} generated successfully ({file.Length:N0} B). ");

                return 0;
            }
            catch (Exception exception)
            {
                console.Error.WriteLine($"An error occurred: {exception}");
                return 2;
            }
        }

        private static async Task GenerateClientSourceCode(ProgramOptions options, List<FileInfo> generatedFiles)
        {
            GraphQlSchema schema;
            if (String.IsNullOrWhiteSpace(options.ServiceUrl))
                schema = GraphQlGenerator.DeserializeGraphQlSchema(await File.ReadAllTextAsync(options.SchemaFileName));
            else
            {
                if (!KeyValueParameterParser.TryGetCustomHeaders(options.Header, out var headers, out var headerParsingErrorMessage))
                    throw new InvalidOperationException(headerParsingErrorMessage);

                schema = await GraphQlGenerator.RetrieveSchema(new HttpMethod(options.HttpMethod), options.ServiceUrl, headers);
            }
            
            var generatorConfiguration =
                new GraphQlGeneratorConfiguration
                {
                    CSharpVersion = options.CSharpVersion,
                    ClassPrefix = options.ClassPrefix,
                    ClassSuffix = options.ClassSuffix,
                    GeneratePartialClasses = options.PartialClasses,
                    MemberAccessibility = options.MemberAccessibility,
                    IdTypeMapping = options.IdTypeMapping,
                    FloatTypeMapping = options.FloatTypeMapping,
                    JsonPropertyGeneration = options.JsonPropertyAttribute,
                    EnumTypeNaming = options.EnumTypeNaming
                };

            if (!KeyValueParameterParser.TryGetCustomClassMapping(options.ClassMapping, out var customMapping, out var customMappingParsingErrorMessage))
                throw new InvalidOperationException(customMappingParsingErrorMessage);

            foreach (var kvp in customMapping)
                generatorConfiguration.CustomClassNameMapping.Add(kvp);
            
            var generator = new GraphQlGenerator(generatorConfiguration);

            if (options.OutputType == OutputType.SingleFile)
            {
                await File.WriteAllTextAsync(options.OutputPath, generator.GenerateFullClientCSharpFile(schema, options.Namespace));
                generatedFiles.Add(new FileInfo(options.OutputPath));
            }
            else
            {
                var multipleFileGenerationContext = new MultipleFileGenerationContext(schema, options.OutputPath, options.Namespace);
                generator.Generate(multipleFileGenerationContext);
                generatedFiles.AddRange(multipleFileGenerationContext.Files);
            }
        }
    }
}