// Stand-ins for the NuGet packages the backend needs but cannot restore here.
//
// WHY THIS FILE EXISTS - and what it fixed
// ----------------------------------------
// The C# compiler binds a compilation in phases: parse, then declarations, then
// method bodies. If ANY declaration-level error exists, csc reports it and never
// binds a single method body.
//
// scripts/typecheck-offline.sh always had 12 such errors - the JwtBearer handler,
// Swashbuckle's OpenApi types, and the System.CommandLine `Command` base class
// that every CLI command derives from. So for as long as it has existed, that
// script checked DECLARATIONS ONLY. Its own header claimed it caught "wrong
// method names, wrong argument counts" - both of which are method-body errors,
// and neither of which it could see. Proof, if wanted:
//
//     echo 'public class B { public void M() { int x = "no"; } }' > body.cs
//     echo 'using Missing.Namespace;' > decl.cs
//     csc body.cs           -> 1 error
//     csc body.cs decl.cs   -> 1 error, and it is decl.cs's
//
// Declaring these types brings the declaration-error count to zero, which is
// what lets the compiler reach the bodies. That is the entire point: it is not
// about these packages, it is about everything downstream of them.
//
// HOW FAITHFUL THIS IS
// --------------------
// Deliberately a little more permissive than the real APIs in places -
// SetHandler here accepts return types the real overloads may not. A stub that
// is too strict invents errors in correct code, which is worse than one that is
// too loose: the real `dotnet build` in CI is what checks the frameworks
// themselves. What this file must never do is change how the compiler sees a
// LOCO type, and it does not - no Loco type is mentioned here.
//
// Compiled only by scripts/typecheck-offline.sh. Never part of a real build.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace System.CommandLine
{
    /// <summary>Anything a handler can be bound to.</summary>
    public interface IValueDescriptor { }
    public interface IValueDescriptor<T> : IValueDescriptor { }

    public class Symbol
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }

    public class Option : Symbol, IValueDescriptor
    {
        public bool IsRequired { get; set; }
        public void AddAlias(string alias) { }
    }

    public class Option<T> : Option, IValueDescriptor<T>
    {
        public Option(string name, string? description = null) { Name = name; Description = description; }
        public Option(string name, Func<T> getDefaultValue, string? description = null) { Name = name; Description = description; }
        public Option(string[] aliases, string? description = null) { Description = description; }
        public Option(string[] aliases, Func<T> getDefaultValue, string? description = null) { Description = description; }
        public void SetDefaultValue(object? value) { }
    }

    public class Argument : Symbol, IValueDescriptor { }

    public class Argument<T> : Argument, IValueDescriptor<T>
    {
        public Argument(string name, string? description = null) { Name = name; Description = description; }
        public Argument(string name, Func<T> getDefaultValue, string? description = null) { Name = name; Description = description; }
        public Argument() { }
        public void SetDefaultValue(object? value) { }
    }

    public class Command : Symbol
    {
        public Command(string name, string? description = null) { Name = name; Description = description; }

        public void AddOption(Option option) { }
        public void AddGlobalOption(Option option) { }
        public void AddArgument(Argument argument) { }
        public void AddCommand(Command command) { }
        public void AddAlias(string alias) { }

        // Arity 0
        public void SetHandler(Action handler) { }
        public void SetHandler(Func<int> handler) { }
        public void SetHandler(Func<Task> handler) { }
        public void SetHandler(Func<Task<int>> handler) { }

        // Arity 1
        public void SetHandler<T1>(Action<T1> handler, IValueDescriptor<T1> s1) { }
        public void SetHandler<T1>(Func<T1, int> handler, IValueDescriptor<T1> s1) { }
        public void SetHandler<T1>(Func<T1, Task> handler, IValueDescriptor<T1> s1) { }
        public void SetHandler<T1>(Func<T1, Task<int>> handler, IValueDescriptor<T1> s1) { }

        // Arity 2
        public void SetHandler<T1, T2>(Action<T1, T2> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2) { }
        public void SetHandler<T1, T2>(Func<T1, T2, int> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2) { }
        public void SetHandler<T1, T2>(Func<T1, T2, Task> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2) { }
        public void SetHandler<T1, T2>(Func<T1, T2, Task<int>> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2) { }

        // Arity 3
        public void SetHandler<T1, T2, T3>(Action<T1, T2, T3> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3) { }
        public void SetHandler<T1, T2, T3>(Func<T1, T2, T3, int> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3) { }
        public void SetHandler<T1, T2, T3>(Func<T1, T2, T3, Task> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3) { }
        public void SetHandler<T1, T2, T3>(Func<T1, T2, T3, Task<int>> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3) { }

        // Arity 4
        public void SetHandler<T1, T2, T3, T4>(Action<T1, T2, T3, T4> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4) { }
        public void SetHandler<T1, T2, T3, T4>(Func<T1, T2, T3, T4, int> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4) { }
        public void SetHandler<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4) { }
        public void SetHandler<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task<int>> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4) { }

        // Arity 5
        public void SetHandler<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5) { }
        public void SetHandler<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, int> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5) { }
        public void SetHandler<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5) { }
        public void SetHandler<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task<int>> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5) { }

        // Arity 6
        public void SetHandler<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6) { }
        public void SetHandler<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, int> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6) { }
        public void SetHandler<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6) { }
        public void SetHandler<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task<int>> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6) { }

        // Arity 7
        public void SetHandler<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6, IValueDescriptor<T7> s7) { }
        public void SetHandler<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, int> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6, IValueDescriptor<T7> s7) { }
        public void SetHandler<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6, IValueDescriptor<T7> s7) { }
        public void SetHandler<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task<int>> handler, IValueDescriptor<T1> s1, IValueDescriptor<T2> s2, IValueDescriptor<T3> s3, IValueDescriptor<T4> s4, IValueDescriptor<T5> s5, IValueDescriptor<T6> s6, IValueDescriptor<T7> s7) { }
    }

    public class RootCommand : Command
    {
        public RootCommand(string? description = null) : base("root", description) { }
    }

    /// <summary>`command.InvokeAsync(args)` - an extension in the real package.</summary>
    public static class CommandExtensions
    {
        public static Task<int> InvokeAsync(this Command command, string[] args) => Task.FromResult(0);
        public static Task<int> InvokeAsync(this Command command, string args) => Task.FromResult(0);
        public static int Invoke(this Command command, string[] args) => 0;
    }

    public class ParseResult
    {
        public T? GetValueForOption<T>(Option<T> option) => default;
        public T? GetValueForArgument<T>(Argument<T> argument) => default;
    }
}

namespace System.CommandLine.Invocation
{
    public class InvocationContext
    {
        public System.CommandLine.ParseResult ParseResult => new();
        public int ExitCode { get; set; }
    }
}

namespace Microsoft.AspNetCore.Authentication.JwtBearer
{
    public static class JwtBearerDefaults
    {
        public const string AuthenticationScheme = "Bearer";
    }

    public class JwtBearerOptions
    {
        public Microsoft.IdentityModel.Tokens.TokenValidationParameters TokenValidationParameters { get; set; } = new();
        public bool RequireHttpsMetadata { get; set; }
        public string? Authority { get; set; }
        public string? Audience { get; set; }
        public bool SaveToken { get; set; }
        public JwtBearerEvents Events { get; set; } = new();
    }

    public class JwtBearerEvents
    {
        public Func<object, Task> OnAuthenticationFailed { get; set; } = _ => Task.CompletedTask;
        public Func<object, Task> OnTokenValidated { get; set; } = _ => Task.CompletedTask;
        public Func<object, Task> OnChallenge { get; set; } = _ => Task.CompletedTask;
    }

    public static class JwtBearerExtensions
    {
        public static Microsoft.AspNetCore.Authentication.AuthenticationBuilder AddJwtBearer(
            this Microsoft.AspNetCore.Authentication.AuthenticationBuilder builder) => builder;

        public static Microsoft.AspNetCore.Authentication.AuthenticationBuilder AddJwtBearer(
            this Microsoft.AspNetCore.Authentication.AuthenticationBuilder builder,
            Action<JwtBearerOptions> configure) => builder;

        public static Microsoft.AspNetCore.Authentication.AuthenticationBuilder AddJwtBearer(
            this Microsoft.AspNetCore.Authentication.AuthenticationBuilder builder,
            string scheme,
            Action<JwtBearerOptions> configure) => builder;
    }
}

namespace Microsoft.OpenApi.Models
{
    public enum ReferenceType { SecurityScheme, Schema, Response, Parameter }
    public enum SecuritySchemeType { ApiKey, Http, OAuth2, OpenIdConnect }
    public enum ParameterLocation { Query, Header, Path, Cookie }

    public class OpenApiInfo
    {
        public string? Title { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
        public OpenApiContact? Contact { get; set; }
        public OpenApiLicense? License { get; set; }
    }

    public class OpenApiContact
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public Uri? Url { get; set; }
    }

    public class OpenApiLicense
    {
        public string? Name { get; set; }
        public Uri? Url { get; set; }
    }

    public class OpenApiReference
    {
        public ReferenceType Type { get; set; }
        public string? Id { get; set; }
    }

    public class OpenApiSecurityScheme
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public SecuritySchemeType Type { get; set; }
        public ParameterLocation In { get; set; }
        public string? Scheme { get; set; }
        public string? BearerFormat { get; set; }
        public OpenApiReference? Reference { get; set; }
    }

    public class OpenApiSecurityRequirement : Dictionary<OpenApiSecurityScheme, IList<string>> { }
}


namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class SwaggerGenOptions
    {
        public void SwaggerDoc(string name, Microsoft.OpenApi.Models.OpenApiInfo info) { }
        public void AddSecurityDefinition(string name, Microsoft.OpenApi.Models.OpenApiSecurityScheme scheme) { }
        public void AddSecurityRequirement(Microsoft.OpenApi.Models.OpenApiSecurityRequirement requirement) { }
        public void IncludeXmlComments(string filePath, bool includeControllerXmlComments = false) { }
        public void EnableAnnotations() { }
    }
}

namespace Swashbuckle.AspNetCore.SwaggerUI
{
    public class SwaggerUIOptions
    {
        public string RoutePrefix { get; set; } = "";
        public string DocumentTitle { get; set; } = "";
        public void SwaggerEndpoint(string url, string name) { }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class SwaggerGenServiceCollectionExtensions
    {
        public static IServiceCollection AddSwaggerGen(this IServiceCollection services) => services;

        public static IServiceCollection AddSwaggerGen(
            this IServiceCollection services,
            Action<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions> setup) => services;
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class SwaggerBuilderExtensions
    {
        public static IApplicationBuilder UseSwagger(this IApplicationBuilder app) => app;

        public static IApplicationBuilder UseSwaggerUI(this IApplicationBuilder app) => app;

        public static IApplicationBuilder UseSwaggerUI(
            this IApplicationBuilder app,
            Action<Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIOptions> setup) => app;
    }
}
