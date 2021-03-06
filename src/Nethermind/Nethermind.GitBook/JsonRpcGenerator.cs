using System.IO;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Nethermind.JsonRpc.Modules;
using System.Text;

namespace Nethermind.GitBook
{
    public class JsonRpcGenerator
    {
        public void Generate()
        {
            string docsDir = DocsDirFinder.FindJsonRpc();
            List<Type> rpcTypes = GetRpcModules();

            foreach (Type rpcType in rpcTypes)
            {
                GenerateDocFileContent(rpcType, docsDir);
            }
        }

        private List<Type> GetRpcModules()
        {
            Assembly assembly = Assembly.Load("Nethermind.JsonRpc");
            List<Type> jsonRpcModules = new List<Type>();

            foreach (Type type in assembly.GetTypes()
                    .Where(t => typeof(IModule).IsAssignableFrom(t))
                    .Where(t => t.IsInterface && t != typeof(IModule)))
            {
                jsonRpcModules.Add(type);
            }

            return jsonRpcModules;
        }

        private void GenerateDocFileContent(Type rpcType, string docsDir)
        {
            StringBuilder docBuilder = new StringBuilder();

            string moduleName = rpcType.Name.Substring(1).Replace("Module", "").ToLower();
            MethodInfo[] moduleMethods = rpcType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            List<Type> rpcTypesToDescribe;

            docBuilder.AppendLine(@$"#{moduleName}");
            docBuilder.AppendLine();

            foreach (MethodInfo method in moduleMethods)
            {
                JsonRpcMethodAttribute attribute = method.GetCustomAttribute<JsonRpcMethodAttribute>();
                bool isImplemented = attribute == null || attribute.IsImplemented;

                if (!isImplemented)
                {
                    continue;
                }

                string methodName = method.Name.Substring(method.Name.IndexOf('_'));
                docBuilder.AppendLine(@$"##{moduleName}\{methodName}");
                docBuilder.AppendLine();
                docBuilder.AppendLine(@$"{attribute?.Description ?? "_description missing_"} ");
                docBuilder.AppendLine();
                docBuilder.AppendLine(@"#### **Parameters**");
                docBuilder.AppendLine();

                ParameterInfo[] parameters = method.GetParameters();
                rpcTypesToDescribe = new List<Type>();

                if (parameters.Length == 0)
                {
                    docBuilder.AppendLine("_None_");
                }
                else
                {
                    docBuilder.AppendLine("| Parameter name | Type |");
                    docBuilder.AppendLine("| :--- | :--- |");
                    string rpcParameterType;
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        rpcParameterType = GetJsonRpcType(parameter.ParameterType, rpcTypesToDescribe);
                        docBuilder.AppendLine($"| {parameter.Name} | `{rpcParameterType}` |");
                    }
                }

                docBuilder.AppendLine();
                docBuilder.AppendLine(@$"#### Return type");

                //assume that there will be only one generic argument in each rpc method return type
                Type returnType = method.ReturnType.GetGenericArguments()[0];
                string returnRpcType = GetJsonRpcType(returnType, rpcTypesToDescribe);

                docBuilder.AppendLine(@$"`{returnRpcType}`");
                docBuilder.AppendLine();

                if (rpcTypesToDescribe.Count != 0)
                {
                    AddRpcObjectsDescription(docBuilder, rpcTypesToDescribe);
                }

            }

            string rpcModuleFile = Directory.GetFiles(docsDir, $"{moduleName}.md", SearchOption.AllDirectories).First();

            string fileContent = docBuilder.ToString();
            File.WriteAllText(rpcModuleFile, fileContent);
        }

        private string GetJsonRpcType(Type type, List<Type> rpcTypesToDescribe)
        {
            var rpcType = type.Name switch
            {
                "Byte[]" => "Data",
                "String" => "String",
                "UInt256" => "Quantity",
                "Address" => "Address",
                "Boolean" => "Boolean",
                "Int32" => "Quantity",
                "Int64" => "Quantity",
                "Keccak" => "Hash",
                "String[]" => "Array",
                _ => $"{type.Name} object",
            };

            if (rpcType.Equals($"{type.Name} object") && rpcTypesToDescribe != null)
            {
                rpcTypesToDescribe.Add(type);
            }

            return rpcType;
        }

        private void AddRpcObjectsDescription(StringBuilder rpcModuleBuilder, List<Type> rpcTypesToDescribe)
        {
            rpcModuleBuilder.AppendLine(@$"#### Objects definition");
            rpcModuleBuilder.AppendLine();

            foreach (Type rpcType in rpcTypesToDescribe)
            {
                rpcModuleBuilder.AppendLine(@$"`{rpcType.Name}`");
                PropertyInfo[] properties = rpcType.GetProperties();

                rpcModuleBuilder.AppendLine("| Fields name | Type |");
                rpcModuleBuilder.AppendLine("| :--- | :--- |");

                string propertyRpcType;
                foreach (PropertyInfo property in properties)
                {
                    propertyRpcType = GetJsonRpcType(property.PropertyType, null);
                    rpcModuleBuilder.AppendLine($"| {property.Name} | `{propertyRpcType}` |");
                }

                rpcModuleBuilder.AppendLine();
            }
        }
    }
}