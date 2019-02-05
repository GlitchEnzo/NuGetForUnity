using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.CompilerServices.SymbolWriter;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NuGet.Unity
{
    public static class DebugSymbolUtility
    {
        public static void GenerateMdbFromAssembly(string assemblyPath)
        {
            var generatedAssemblyPath = $"{assemblyPath}New.dll";
            var generatedMdbPath = $"{generatedAssemblyPath}.mdb";
            var generatedMdbRelocationPath = $"{assemblyPath}.mdb";
            var pdbPath = assemblyPath.Replace(".dll", ".pdb");

            if(File.Exists(generatedAssemblyPath))
            {
                File.Delete(generatedAssemblyPath);
            }

            if(File.Exists(generatedMdbPath))
            {
                File.Delete(generatedMdbPath);
            }

            if (File.Exists(generatedMdbRelocationPath))
            {
                File.Delete(generatedMdbRelocationPath);
            }

            var reader_parameters = new ReaderParameters
            {
                SymbolReaderProvider = new PortablePdbReaderProvider(),
            };

            using (var module = ModuleDefinition.ReadModule(assemblyPath, reader_parameters))
            {
                var writer_parameters = new WriterParameters
                {
                    SymbolWriterProvider = new MdbWriterProvider(),
                };

                module.Write(generatedAssemblyPath, writer_parameters);
            }

            // Force delete the dll from Library...
            AssetDatabase.DeleteAsset(GetAssetsRelativePath(assemblyPath));
            AssetDatabase.DeleteAsset(GetAssetsRelativePath(pdbPath));

            File.Move(generatedAssemblyPath, assemblyPath);
            File.Move(generatedMdbPath, generatedMdbRelocationPath);

            AssetDatabase.ImportAsset(GetAssetsRelativePath(generatedMdbRelocationPath), ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(GetAssetsRelativePath(assemblyPath), ImportAssetOptions.ForceUpdate);
        }

        public static string GetAssetsRelativePath(string originalPath)
        {
            var originalPathNormalized = originalPath.Replace("\\", "/");

            string[] assetsSplit = originalPathNormalized.Split(new string[] { "Assets/" }, StringSplitOptions.None);

            if(assetsSplit.Length > 0)
            {
                Debug.Log($"Assets/{assetsSplit[1]}");

                return $"Assets/{assetsSplit[1]}";
            }

            return originalPathNormalized;
        }

        public static void MdbRebase(string mdbPath, string sourceDirectory, string[] sourceFilePaths)
        {
            var mdbInput = MonoSymbolFile.ReadSymbolFile(mdbPath);
            var mdbOutput = new MonoSymbolFile();
          
            foreach(var sourceFilePath in sourceFilePaths)
            {
                foreach(var mdbSource in mdbInput.Sources)
                {
                    var normalizedMdbSourceFilePath = mdbSource.FileName.Replace("\\", "/");
                    var normalizedSourceFilePath = sourceFilePath.Replace("\\", "/");

                    // Found the source file, alter the path...
                    if(normalizedMdbSourceFilePath.EndsWith(normalizedSourceFilePath))
                    {
                        mdbSource.FileName = $"{sourceDirectory}{sourceFilePath}";
                        mdbOutput.AddSource(mdbSource);
                        Debug.Log($"{normalizedMdbSourceFilePath} => { mdbSource.FileName}");
                    }
                }
            }

            foreach (var cu in mdbInput.CompileUnits)
            {
                cu.ReadAll();
                mdbOutput.AddCompileUnit(cu);
            }

            foreach (var m in mdbInput.Methods)
            {
                m.ReadAll();
                mdbOutput.AddMethod(m);
            }

            mdbInput.Dispose();
            File.Delete(mdbPath);

            using (var stream = new FileStream(mdbPath, FileMode.Create))
            {
                mdbOutput.CreateSymbolFile(mdbInput.Guid, stream);
            }
        }
    }
}
