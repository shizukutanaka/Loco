using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace Loco.Core.Testing;

/// <summary>
/// Automatic test generator using Roslyn
/// Generates unit tests, integration tests, and performance benchmarks
/// </summary>
public sealed class AutoTestGenerator
{
    private readonly ILogger<AutoTestGenerator> _logger;
    private readonly Dictionary<string, TestTemplate> _templates;
    private readonly TestAnalyzer _analyzer;
    
    private class TestTemplate
    {
        public string Name { get; set; }
        public string Template { get; set; }
        public TestType Type { get; set; }
        public string[] RequiredUsings { get; set; }
    }
    
    public enum TestType
    {
        Unit,
        Integration,
        Performance,
        Stress,
        Security
    }
    
    public AutoTestGenerator(ILogger<AutoTestGenerator> logger)
    {
        _logger = logger;
        _templates = InitializeTemplates();
        _analyzer = new TestAnalyzer(logger);
    }
    
    /// <summary>
    /// Generate tests for a class
    /// </summary>
    public async Task<GeneratedTests> GenerateTestsAsync(
        string sourceCode,
        TestGenerationOptions options = null)
    {
        options ??= new TestGenerationOptions();
        
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = await tree.GetRootAsync();
        
        var result = new GeneratedTests
        {
            Namespace = options.TestNamespace ?? "Tests.Generated",
            FileName = options.FileName ?? "GeneratedTests.cs"
        };
        
        // Find all classes
        var classes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));
        
        foreach (var classDecl in classes)
        {
            var classTests = await GenerateClassTestsAsync(classDecl, options);
            result.TestClasses.Add(classTests);
        }
        
        // Generate complete test file
        result.SourceCode = GenerateTestFile(result);
        
        _logger.LogInformation("Generated {Count} test classes", result.TestClasses.Count);
        
        return result;
    }
    
    /// <summary>
    /// Generate tests for an assembly
    /// </summary>
    public async Task<List<GeneratedTests>> GenerateAssemblyTestsAsync(
        Assembly assembly,
        TestGenerationOptions options = null)
    {
        options ??= new TestGenerationOptions();
        var results = new List<GeneratedTests>();
        
        var types = assembly.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);
        
        foreach (var type in types)
        {
            try
            {
                var tests = await GenerateTypeTestsAsync(type, options);
                if (tests != null)
                    results.Add(tests);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate tests for type {Type}", type.Name);
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Generate performance benchmarks
    /// </summary>
    public string GenerateBenchmarks(Type type)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using BenchmarkDotNet.Attributes;");
        sb.AppendLine("using BenchmarkDotNet.Running;");
        sb.AppendLine($"using {type.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {type.Namespace}.Benchmarks");
        sb.AppendLine("{");
        sb.AppendLine($"    [MemoryDiagnoser]");
        sb.AppendLine($"    [SimpleJob(warmupCount: 3, targetCount: 10)]");
        sb.AppendLine($"    public class {type.Name}Benchmarks");
        sb.AppendLine("    {");
        sb.AppendLine($"        private {type.Name} _instance;");
        sb.AppendLine();
        sb.AppendLine("        [GlobalSetup]");
        sb.AppendLine("        public void Setup()");
        sb.AppendLine("        {");
        sb.AppendLine($"            _instance = new {type.Name}();");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Generate benchmark for each public method
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.IsSpecialName || method.DeclaringType != type)
                continue;
            
            sb.AppendLine($"        [Benchmark]");
            sb.AppendLine($"        public void Benchmark_{method.Name}()");
            sb.AppendLine("        {");
            
            var parameters = GenerateDefaultParameters(method);
            sb.AppendLine($"            _instance.{method.Name}({parameters});");
            
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate property-based tests
    /// </summary>
    public string GeneratePropertyTests(Type type)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using FsCheck;");
        sb.AppendLine("using FsCheck.Xunit;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine($"using {type.Namespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {type.Namespace}.PropertyTests");
        sb.AppendLine("{");
        sb.AppendLine($"    public class {type.Name}PropertyTests");
        sb.AppendLine("    {");
        
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.IsSpecialName || method.DeclaringType != type)
                continue;
            
            // Generate property test for pure functions
            if (IsPureFunction(method))
            {
                sb.AppendLine($"        [Property]");
                sb.AppendLine($"        public void {method.Name}_ShouldBeIdempotent(");
                
                var parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    var param = parameters[i];
                    sb.Append($"            {GetPropertyTestType(param.ParameterType)} {param.Name}");
                    if (i < parameters.Length - 1)
                        sb.Append(",");
                    sb.AppendLine();
                }
                
                sb.AppendLine("        )");
                sb.AppendLine("        {");
                sb.AppendLine($"            var instance = new {type.Name}();");
                sb.AppendLine($"            var result1 = instance.{method.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
                sb.AppendLine($"            var result2 = instance.{method.Name}({string.Join(", ", parameters.Select(p => p.Name))});");
                sb.AppendLine("            Assert.Equal(result1, result2);");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
        }
        
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private async Task<TestClass> GenerateClassTestsAsync(
        ClassDeclarationSyntax classDecl,
        TestGenerationOptions options)
    {
        var testClass = new TestClass
        {
            Name = $"{classDecl.Identifier.Text}Tests",
            OriginalClassName = classDecl.Identifier.Text
        };
        
        // Find all public methods
        var methods = classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)));
        
        foreach (var method in methods)
        {
            var testMethods = GenerateMethodTests(method, classDecl.Identifier.Text, options);
            testClass.TestMethods.AddRange(testMethods);
        }
        
        // Add setup and teardown
        testClass.SetupCode = GenerateSetupCode(classDecl);
        testClass.TeardownCode = GenerateTeardownCode(classDecl);
        
        return testClass;
    }
    
    private List<TestMethod> GenerateMethodTests(
        MethodDeclarationSyntax method,
        string className,
        TestGenerationOptions options)
    {
        var tests = new List<TestMethod>();
        
        // Happy path test
        tests.Add(new TestMethod
        {
            Name = $"{method.Identifier.Text}_Should_Succeed_With_Valid_Input",
            TestType = TestType.Unit,
            Code = GenerateHappyPathTest(method, className)
        });
        
        // Null parameter tests
        if (options.GenerateNullTests)
        {
            var nullTests = GenerateNullParameterTests(method, className);
            tests.AddRange(nullTests);
        }
        
        // Exception tests
        if (options.GenerateExceptionTests)
        {
            var exceptionTest = GenerateExceptionTest(method, className);
            if (exceptionTest != null)
                tests.Add(exceptionTest);
        }
        
        // Performance test
        if (options.GeneratePerformanceTests)
        {
            tests.Add(new TestMethod
            {
                Name = $"{method.Identifier.Text}_Performance_Test",
                TestType = TestType.Performance,
                Code = GeneratePerformanceTest(method, className)
            });
        }
        
        return tests;
    }
    
    private string GenerateHappyPathTest(MethodDeclarationSyntax method, string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Arrange");
        sb.AppendLine($"var instance = new {className}();");
        
        foreach (var param in method.ParameterList.Parameters)
        {
            sb.AppendLine($"var {param.Identifier.Text} = {GenerateDefaultValue(param.Type)};");
        }
        
        sb.AppendLine();
        sb.AppendLine("// Act");
        
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.Text));
        var hasReturn = method.ReturnType.ToString() != "void";
        
        if (hasReturn)
        {
            sb.AppendLine($"var result = instance.{method.Identifier.Text}({parameters});");
        }
        else
        {
            sb.AppendLine($"instance.{method.Identifier.Text}({parameters});");
        }
        
        sb.AppendLine();
        sb.AppendLine("// Assert");
        
        if (hasReturn)
        {
            sb.AppendLine("Assert.NotNull(result);");
        }
        else
        {
            sb.AppendLine("// Add assertions here");
        }
        
        return sb.ToString();
    }
    
    private List<TestMethod> GenerateNullParameterTests(MethodDeclarationSyntax method, string className)
    {
        var tests = new List<TestMethod>();
        
        foreach (var param in method.ParameterList.Parameters)
        {
            if (!IsNullableType(param.Type))
                continue;
            
            var test = new TestMethod
            {
                Name = $"{method.Identifier.Text}_Should_Handle_Null_{param.Identifier.Text}",
                TestType = TestType.Unit,
                Code = GenerateNullParameterTest(method, className, param)
            };
            
            tests.Add(test);
        }
        
        return tests;
    }
    
    private string GenerateNullParameterTest(
        MethodDeclarationSyntax method,
        string className,
        ParameterSyntax nullParam)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Arrange");
        sb.AppendLine($"var instance = new {className}();");
        
        foreach (var param in method.ParameterList.Parameters)
        {
            if (param == nullParam)
            {
                sb.AppendLine($"{param.Type} {param.Identifier.Text} = null;");
            }
            else
            {
                sb.AppendLine($"var {param.Identifier.Text} = {GenerateDefaultValue(param.Type)};");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("// Act & Assert");
        
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.Text));
        sb.AppendLine($"Assert.Throws<ArgumentNullException>(() => instance.{method.Identifier.Text}({parameters}));");
        
        return sb.ToString();
    }
    
    private TestMethod GenerateExceptionTest(MethodDeclarationSyntax method, string className)
    {
        // Analyze method for potential exceptions
        var exceptions = _analyzer.FindPotentialExceptions(method);
        
        if (!exceptions.Any())
            return null;
        
        var sb = new StringBuilder();
        sb.AppendLine("// Test for exception handling");
        sb.AppendLine($"var instance = new {className}();");
        
        // Generate invalid input that might cause exception
        foreach (var param in method.ParameterList.Parameters)
        {
            sb.AppendLine($"var {param.Identifier.Text} = {GenerateInvalidValue(param.Type)};");
        }
        
        sb.AppendLine();
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.Text));
        sb.AppendLine($"Assert.ThrowsAny<Exception>(() => instance.{method.Identifier.Text}({parameters}));");
        
        return new TestMethod
        {
            Name = $"{method.Identifier.Text}_Should_Handle_Exceptions",
            TestType = TestType.Unit,
            Code = sb.ToString()
        };
    }
    
    private string GeneratePerformanceTest(MethodDeclarationSyntax method, string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var instance = new {className}();");
        
        foreach (var param in method.ParameterList.Parameters)
        {
            sb.AppendLine($"var {param.Identifier.Text} = {GenerateDefaultValue(param.Type)};");
        }
        
        sb.AppendLine();
        sb.AppendLine("var stopwatch = Stopwatch.StartNew();");
        sb.AppendLine("for (int i = 0; i < 1000; i++)");
        sb.AppendLine("{");
        
        var parameters = string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.Text));
        sb.AppendLine($"    instance.{method.Identifier.Text}({parameters});");
        
        sb.AppendLine("}");
        sb.AppendLine("stopwatch.Stop();");
        sb.AppendLine();
        sb.AppendLine("Assert.True(stopwatch.ElapsedMilliseconds < 1000, \"Performance threshold exceeded\");");
        
        return sb.ToString();
    }
    
    private async Task<GeneratedTests> GenerateTypeTestsAsync(Type type, TestGenerationOptions options)
    {
        // Generate tests from runtime type information
        var result = new GeneratedTests
        {
            Namespace = options.TestNamespace ?? $"{type.Namespace}.Tests",
            FileName = $"{type.Name}Tests.cs"
        };
        
        var testClass = new TestClass
        {
            Name = $"{type.Name}Tests",
            OriginalClassName = type.Name
        };
        
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.IsSpecialName || method.DeclaringType != type)
                continue;
            
            var testMethod = new TestMethod
            {
                Name = $"{method.Name}_Test",
                TestType = TestType.Unit,
                Code = GenerateMethodTestFromReflection(method, type)
            };
            
            testClass.TestMethods.Add(testMethod);
        }
        
        result.TestClasses.Add(testClass);
        result.SourceCode = GenerateTestFile(result);
        
        return result;
    }
    
    private string GenerateMethodTestFromReflection(MethodInfo method, Type type)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"var instance = new {type.Name}();");
        
        var parameters = method.GetParameters();
        foreach (var param in parameters)
        {
            sb.AppendLine($"var {param.Name} = default({param.ParameterType.Name});");
        }
        
        var paramNames = string.Join(", ", parameters.Select(p => p.Name));
        
        if (method.ReturnType != typeof(void))
        {
            sb.AppendLine($"var result = instance.{method.Name}({paramNames});");
            sb.AppendLine("Assert.NotNull(result);");
        }
        else
        {
            sb.AppendLine($"instance.{method.Name}({paramNames});");
        }
        
        return sb.ToString();
    }
    
    private string GenerateTestFile(GeneratedTests tests)
    {
        var sb = new StringBuilder();
        
        // Usings
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        
        // Namespace
        sb.AppendLine($"namespace {tests.Namespace}");
        sb.AppendLine("{");
        
        foreach (var testClass in tests.TestClasses)
        {
            sb.AppendLine($"    public class {testClass.Name}");
            sb.AppendLine("    {");
            
            // Setup
            if (!string.IsNullOrEmpty(testClass.SetupCode))
            {
                sb.AppendLine("        private " + testClass.OriginalClassName + " _instance;");
                sb.AppendLine();
                sb.AppendLine("        public " + testClass.Name + "()");
                sb.AppendLine("        {");
                sb.AppendLine("            " + testClass.SetupCode);
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            // Test methods
            foreach (var method in testClass.TestMethods)
            {
                sb.AppendLine("        [Fact]");
                sb.AppendLine($"        public void {method.Name}()");
                sb.AppendLine("        {");
                
                var lines = method.Code.Split('\n');
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine($"            {line}");
                }
                
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            sb.AppendLine("    }");
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    private string GenerateSetupCode(ClassDeclarationSyntax classDecl)
    {
        return $"_instance = new {classDecl.Identifier.Text}();";
    }
    
    private string GenerateTeardownCode(ClassDeclarationSyntax classDecl)
    {
        return "(_instance as IDisposable)?.Dispose();";
    }
    
    private string GenerateDefaultValue(TypeSyntax type)
    {
        var typeString = type.ToString();
        
        return typeString switch
        {
            "string" => "\"test\"",
            "int" => "42",
            "long" => "42L",
            "double" => "3.14",
            "float" => "3.14f",
            "bool" => "true",
            "DateTime" => "DateTime.Now",
            "Guid" => "Guid.NewGuid()",
            _ when typeString.EndsWith("[]") => $"new {typeString} {{ }}",
            _ when typeString.StartsWith("List<") => $"new {typeString}()",
            _ when typeString.StartsWith("Dictionary<") => $"new {typeString}()",
            _ => $"new {typeString}()"
        };
    }
    
    private string GenerateInvalidValue(TypeSyntax type)
    {
        var typeString = type.ToString();
        
        return typeString switch
        {
            "string" => "string.Empty",
            "int" => "-1",
            "long" => "-1L",
            "double" => "double.NaN",
            "float" => "float.NaN",
            "bool" => "false",
            _ => "null"
        };
    }
    
    private string GenerateDefaultParameters(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var values = new List<string>();
        
        foreach (var param in parameters)
        {
            values.Add($"default({param.ParameterType.Name})");
        }
        
        return string.Join(", ", values);
    }
    
    private bool IsNullableType(TypeSyntax type)
    {
        var typeString = type.ToString();
        return !typeString.StartsWith("int") && 
               !typeString.StartsWith("long") &&
               !typeString.StartsWith("double") &&
               !typeString.StartsWith("float") &&
               !typeString.StartsWith("bool") &&
               !typeString.StartsWith("byte") &&
               !typeString.StartsWith("char");
    }
    
    private bool IsPureFunction(MethodInfo method)
    {
        // Simplified check - in reality would need more analysis
        return method.ReturnType != typeof(void) &&
               !method.Name.StartsWith("Set") &&
               !method.Name.StartsWith("Update") &&
               !method.Name.StartsWith("Delete");
    }
    
    private string GetPropertyTestType(Type type)
    {
        return type.Name switch
        {
            "String" => "NonEmptyString",
            "Int32" => "PositiveInt",
            "Double" => "NormalFloat",
            _ => type.Name
        };
    }
    
    private Dictionary<string, TestTemplate> InitializeTemplates()
    {
        return new Dictionary<string, TestTemplate>
        {
            ["UnitTest"] = new TestTemplate
            {
                Name = "Unit Test",
                Type = TestType.Unit,
                RequiredUsings = new[] { "Xunit" },
                Template = @"[Fact]
public void {MethodName}_Should_{ExpectedBehavior}()
{
    // Arrange
    {ArrangeCode}
    
    // Act
    {ActCode}
    
    // Assert
    {AssertCode}
}"
            },
            ["IntegrationTest"] = new TestTemplate
            {
                Name = "Integration Test",
                Type = TestType.Integration,
                RequiredUsings = new[] { "Xunit", "Microsoft.Extensions.DependencyInjection" },
                Template = @"[Fact]
public async Task {MethodName}_Integration_Test()
{
    // Arrange
    using var scope = _serviceProvider.CreateScope();
    {ArrangeCode}
    
    // Act
    {ActCode}
    
    // Assert
    {AssertCode}
}"
            }
        };
    }
}

// Supporting classes
public class TestGenerationOptions
{
    public string TestNamespace { get; set; }
    public string FileName { get; set; }
    public bool GenerateNullTests { get; set; } = true;
    public bool GenerateExceptionTests { get; set; } = true;
    public bool GeneratePerformanceTests { get; set; } = false;
    public bool GeneratePropertyTests { get; set; } = false;
}

public class GeneratedTests
{
    public string Namespace { get; set; }
    public string FileName { get; set; }
    public List<TestClass> TestClasses { get; set; } = new();
    public string SourceCode { get; set; }
}

public class TestClass
{
    public string Name { get; set; }
    public string OriginalClassName { get; set; }
    public List<TestMethod> TestMethods { get; set; } = new();
    public string SetupCode { get; set; }
    public string TeardownCode { get; set; }
}

public class TestMethod
{
    public string Name { get; set; }
    public AutoTestGenerator.TestType TestType { get; set; }
    public string Code { get; set; }
}

public class TestAnalyzer
{
    private readonly ILogger _logger;
    
    public TestAnalyzer(ILogger logger)
    {
        _logger = logger;
    }
    
    public List<string> FindPotentialExceptions(MethodDeclarationSyntax method)
    {
        var exceptions = new List<string>();
        
        // Look for throw statements
        var throwStatements = method.DescendantNodes()
            .OfType<ThrowStatementSyntax>();
        
        foreach (var throwStmt in throwStatements)
        {
            if (throwStmt.Expression is ObjectCreationExpressionSyntax creation)
            {
                exceptions.Add(creation.Type.ToString());
            }
        }
        
        return exceptions;
    }
}
