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

// ---------------------------------------------------------------------------
// System.CommandLine
//
// This one PARSES. It is not the real package - it is the subset the CLI
// actually uses, implemented for real: subcommand dispatch, --option value
// with aliases, bool flags, positional arguments in declaration order,
// defaults from getDefaultValue, and handler invocation with typed values.
//
// It has to parse, because a stub that returned 0 from InvokeAsync gave the
// entire CLI - about 8,300 lines - zero runtime coverage, and the tests that
// sat on top of it were tautologies asserting that string literals equalled
// themselves. An assertion can only mean something if the code underneath it
// actually ran.
//
// The semantics implemented here are the ones the real System.CommandLine
// also has for this subset, so a test that passes against this parser is
// asserting behaviour that holds in CI against the real package too. What is
// deliberately absent: validators, arity control, response files, suggestions,
// middleware - nothing in src/Loco.Cli touches any of it.
// ---------------------------------------------------------------------------
namespace System.CommandLine
{
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

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

        internal readonly List<string> AliasList = new();

        public void AddAlias(string alias)
        {
            if (!AliasList.Contains(alias)) AliasList.Add(alias);
        }

        internal bool Matches(string token) => AliasList.Contains(token);

        /// <summary>A bool option is a flag: present means true, no value token.</summary>
        internal virtual Type ValueType => typeof(string);
        internal virtual object? ConvertToken(string raw) => raw;
        internal virtual object? DefaultValue() => null;
    }

    public class Option<T> : Option, IValueDescriptor<T>
    {
        private Func<T>? _getDefaultValue;

        public Option(string name, string? description = null) =>
            Init(new[] { name }, null, description);

        public Option(string name, Func<T> getDefaultValue, string? description = null) =>
            Init(new[] { name }, getDefaultValue, description);

        public Option(string[] aliases, string? description = null) =>
            Init(aliases, null, description);

        public Option(string[] aliases, Func<T> getDefaultValue, string? description = null) =>
            Init(aliases, getDefaultValue, description);

        private void Init(string[] aliases, Func<T>? getDefaultValue, string? description)
        {
            foreach (var alias in aliases) AddAlias(alias);
            Name = aliases.Length > 0 ? aliases[0] : "";
            Description = description;
            _getDefaultValue = getDefaultValue;
        }

        public void SetDefaultValue(object? value) { }

        internal override Type ValueType => typeof(T);
        internal override object? ConvertToken(string raw) => Parsing.Convert(typeof(T), raw);
        internal override object? DefaultValue() =>
            _getDefaultValue is null ? default(T) : _getDefaultValue();
    }

    public class Argument : Symbol, IValueDescriptor
    {
        internal virtual object? ConvertToken(string raw) => raw;
        internal virtual bool HasDefault => false;
        internal virtual object? DefaultValue() => null;
    }

    public class Argument<T> : Argument, IValueDescriptor<T>
    {
        private readonly Func<T>? _getDefaultValue;

        public Argument() { }

        public Argument(string name, string? description = null)
        {
            Name = name;
            Description = description;
        }

        public Argument(string name, Func<T> getDefaultValue, string? description = null)
            : this(name, description)
        {
            _getDefaultValue = getDefaultValue;
        }

        public void SetDefaultValue(object? value) { }

        internal override object? ConvertToken(string raw) => Parsing.Convert(typeof(T), raw);
        internal override bool HasDefault => _getDefaultValue is not null;
        internal override object? DefaultValue() =>
            _getDefaultValue is null ? default(T) : _getDefaultValue();
    }

    public class Command : Symbol
    {
        internal readonly List<Option> OptionList = new();
        internal readonly List<Argument> ArgumentList = new();
        internal readonly List<Command> SubcommandList = new();
        internal readonly List<string> AliasList = new();

        internal Delegate? Handler;
        internal IValueDescriptor[] Descriptors = Array.Empty<IValueDescriptor>();

        public Command(string name, string? description = null)
        {
            Name = name;
            Description = description;
        }

        public void AddOption(Option option) => OptionList.Add(option);
        public void AddGlobalOption(Option option) => OptionList.Add(option);
        public void AddArgument(Argument argument) => ArgumentList.Add(argument);
        public void AddCommand(Command command) => SubcommandList.Add(command);
        public void AddAlias(string alias) => AliasList.Add(alias);

        internal bool Matches(string token) =>
            Name == token || AliasList.Contains(token);

        private void Bind(Delegate handler, IValueDescriptor[] descriptors)
        {
            Handler = handler;
            Descriptors = descriptors;
        }

        // Every SetHandler shape the CLI uses funnels into Bind. The generic
        // parameters exist so lambda parameter types are inferred from the
        // descriptors, exactly as with the real package.
        public void SetHandler(Action handle) => Bind(handle, Array.Empty<IValueDescriptor>());
        public void SetHandler(Func<int> handle) => Bind(handle, Array.Empty<IValueDescriptor>());
        public void SetHandler(Func<Task> handle) => Bind(handle, Array.Empty<IValueDescriptor>());
        public void SetHandler(Func<Task<int>> handle) => Bind(handle, Array.Empty<IValueDescriptor>());

        public void SetHandler<T1>(Action<T1> handle, IValueDescriptor<T1> d1) => Bind(handle, new IValueDescriptor[] { d1 });
        public void SetHandler<T1>(Func<T1, int> handle, IValueDescriptor<T1> d1) => Bind(handle, new IValueDescriptor[] { d1 });
        public void SetHandler<T1>(Func<T1, Task> handle, IValueDescriptor<T1> d1) => Bind(handle, new IValueDescriptor[] { d1 });
        public void SetHandler<T1>(Func<T1, Task<int>> handle, IValueDescriptor<T1> d1) => Bind(handle, new IValueDescriptor[] { d1 });

        public void SetHandler<T1, T2>(Action<T1, T2> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2) => Bind(handle, new IValueDescriptor[] { d1, d2 });
        public void SetHandler<T1, T2>(Func<T1, T2, int> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2) => Bind(handle, new IValueDescriptor[] { d1, d2 });
        public void SetHandler<T1, T2>(Func<T1, T2, Task> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2) => Bind(handle, new IValueDescriptor[] { d1, d2 });
        public void SetHandler<T1, T2>(Func<T1, T2, Task<int>> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2) => Bind(handle, new IValueDescriptor[] { d1, d2 });

        public void SetHandler<T1, T2, T3>(Action<T1, T2, T3> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3) => Bind(handle, new IValueDescriptor[] { d1, d2, d3 });
        public void SetHandler<T1, T2, T3>(Func<T1, T2, T3, int> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3) => Bind(handle, new IValueDescriptor[] { d1, d2, d3 });
        public void SetHandler<T1, T2, T3>(Func<T1, T2, T3, Task> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3) => Bind(handle, new IValueDescriptor[] { d1, d2, d3 });
        public void SetHandler<T1, T2, T3>(Func<T1, T2, T3, Task<int>> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3) => Bind(handle, new IValueDescriptor[] { d1, d2, d3 });

        public void SetHandler<T1, T2, T3, T4>(Action<T1, T2, T3, T4> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4 });
        public void SetHandler<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4 });
        public void SetHandler<T1, T2, T3, T4>(Func<T1, T2, T3, T4, Task<int>> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4 });

        public void SetHandler<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4, IValueDescriptor<T5> d5) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4, d5 });
        public void SetHandler<T1, T2, T3, T4, T5>(Func<T1, T2, T3, T4, T5, Task<int>> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4, IValueDescriptor<T5> d5) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4, d5 });

        public void SetHandler<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4, IValueDescriptor<T5> d5, IValueDescriptor<T6> d6) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4, d5, d6 });
        public void SetHandler<T1, T2, T3, T4, T5, T6>(Func<T1, T2, T3, T4, T5, T6, Task<int>> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4, IValueDescriptor<T5> d5, IValueDescriptor<T6> d6) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4, d5, d6 });

        public void SetHandler<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4, IValueDescriptor<T5> d5, IValueDescriptor<T6> d6, IValueDescriptor<T7> d7) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4, d5, d6, d7 });
        public void SetHandler<T1, T2, T3, T4, T5, T6, T7>(Func<T1, T2, T3, T4, T5, T6, T7, Task<int>> handle, IValueDescriptor<T1> d1, IValueDescriptor<T2> d2, IValueDescriptor<T3> d3, IValueDescriptor<T4> d4, IValueDescriptor<T5> d5, IValueDescriptor<T6> d6, IValueDescriptor<T7> d7) => Bind(handle, new IValueDescriptor[] { d1, d2, d3, d4, d5, d6, d7 });
    }

    public class RootCommand : Command
    {
        public RootCommand(string? description = null) : base("root", description) { }
    }

    public static class CommandExtensions
    {
        public static Task<int> InvokeAsync(this Command command, string[] args) =>
            Parsing.InvokeAsync(command, args);

        public static Task<int> InvokeAsync(this Command command, string args) =>
            Parsing.InvokeAsync(command, args.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        public static int Invoke(this Command command, string[] args) =>
            Parsing.InvokeAsync(command, args).GetAwaiter().GetResult();
    }

    /// <summary>The parser behind InvokeAsync.</summary>
    internal static class Parsing
    {
        internal static async Task<int> InvokeAsync(Command command, string[] args)
        {
            // Subcommand dispatch first, as the real package does: the first
            // token wins if it names a subcommand.
            if (args.Length > 0)
            {
                var sub = command.SubcommandList.FirstOrDefault(c => c.Matches(args[0]));
                if (sub is not null)
                {
                    return await InvokeAsync(sub, args[1..]);
                }
            }

            var values = new Dictionary<Symbol, object?>();
            var positionals = new List<string>();

            for (var i = 0; i < args.Length; i++)
            {
                var token = args[i];

                if (token.StartsWith('-'))
                {
                    var option = command.OptionList.FirstOrDefault(o => o.Matches(token));
                    if (option is null)
                    {
                        Console.Error.WriteLine($"Unrecognized option '{token}'.");
                        return 1;
                    }

                    if (option.ValueType == typeof(bool))
                    {
                        values[option] = true;
                        continue;
                    }

                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine($"Option '{token}' expects a value.");
                        return 1;
                    }

                    var converted = option.ConvertToken(args[++i]);
                    if (converted is ConversionFailure failure)
                    {
                        Console.Error.WriteLine(
                            $"Cannot parse '{failure.Raw}' for option '{token}'.");
                        return 1;
                    }

                    values[option] = converted;
                    continue;
                }

                positionals.Add(token);
            }

            if (positionals.Count > command.ArgumentList.Count)
            {
                Console.Error.WriteLine(
                    $"Unrecognized command or argument '{positionals[command.ArgumentList.Count]}'.");
                return 1;
            }

            for (var i = 0; i < command.ArgumentList.Count; i++)
            {
                var argument = command.ArgumentList[i];

                if (i < positionals.Count)
                {
                    var converted = argument.ConvertToken(positionals[i]);
                    if (converted is ConversionFailure failure)
                    {
                        Console.Error.WriteLine(
                            $"Cannot parse '{failure.Raw}' for argument '{argument.Name}'.");
                        return 1;
                    }

                    values[argument] = converted;
                }
                else if (argument.HasDefault)
                {
                    values[argument] = argument.DefaultValue();
                }
                else
                {
                    // The real package treats an argument without a default as
                    // required; missing one is a parse error, not a null.
                    Console.Error.WriteLine($"Required argument '{argument.Name}' missing.");
                    return 1;
                }
            }

            if (command.Handler is null)
            {
                // A command that exists only to hold subcommands, invoked bare.
                Console.Error.WriteLine($"Required command was not provided for '{command.Name}'.");
                return 1;
            }

            var handlerArgs = command.Descriptors
                .Select(d => d switch
                {
                    Option o => values.TryGetValue(o, out var v) ? v : o.DefaultValue(),
                    Argument a => values.TryGetValue(a, out var v) ? v : a.DefaultValue(),
                    _ => null,
                })
                .ToArray();

            object? result;
            try
            {
                result = command.Handler.DynamicInvoke(handlerArgs);
            }
            catch (TargetInvocationException wrapper) when (wrapper.InnerException is not null)
            {
                // The real package's default middleware reports the exception
                // and yields a non-zero exit rather than crashing the process.
                Console.Error.WriteLine(wrapper.InnerException.Message);
                return 1;
            }

            switch (result)
            {
                case int code:
                    return code;
                case Task<int> taskOfInt:
                    return await taskOfInt;
                case Task task:
                    await task;
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// String-to-T for the four types the CLI declares: string, string?,
        /// bool, int. A failure is a sentinel rather than an exception so the
        /// parser can report which token and which symbol.
        /// </summary>
        internal static object? Convert(Type target, string raw)
        {
            target = Nullable.GetUnderlyingType(target) ?? target;

            if (target == typeof(string)) return raw;
            if (target == typeof(bool))
                return bool.TryParse(raw, out var b) ? b : new ConversionFailure(raw);
            if (target == typeof(int))
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                    ? i
                    : new ConversionFailure(raw);

            return new ConversionFailure(raw);
        }

        internal sealed record ConversionFailure(string Raw);
    }
}

namespace System.CommandLine.Invocation
{
    public class InvocationContext
    {
        public ParseResult ParseResult => new();
        public int ExitCode { get; set; }
    }

    public class ParseResult
    {
        public T? GetValueForOption<T>(Option<T> option) => default;
        public T? GetValueForArgument<T>(Argument<T> argument) => default;
    }
}

// ---------------------------------------------------------------------------
// Microsoft.AspNetCore.Authentication.JwtBearer
//
// This one is NOT inert. The real JWT implementation is on disk - the SDK ships
// System.IdentityModel.Tokens.Jwt and Microsoft.IdentityModel.* inside its
// dotnet-user-jwts tool - so tokens are created, signed and validated by
// Microsoft's own code, not by anything written here.
//
// What IS written here is the ASP.NET plumbing the package would otherwise
// provide: pull the bearer token off the request, hand it to
// JwtSecurityTokenHandler.ValidateToken with the TokenValidationParameters the
// application itself configured, and turn the result into an
// AuthenticationTicket. AuthenticationHandler, AuthenticationTicket and the
// authorization policies that consume the resulting claims are all the real
// types from the ASP.NET Core shared framework.
//
// So a test that gets a 401 here gets it because a token failed Microsoft's
// validation, and a test that reaches a controller reached it because the
// framework's own policy evaluation let it through. The seam is narrow and
// worth naming: the real package also handles OIDC discovery, JWKS refresh,
// and the Events hooks below, none of which this does. Nothing in Loco uses
// them - the API validates a symmetric key it holds itself.
// ---------------------------------------------------------------------------
namespace Microsoft.AspNetCore.Authentication.JwtBearer
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Text.Encodings.Web;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;

    public static class JwtBearerDefaults
    {
        public const string AuthenticationScheme = "Bearer";
    }

    public class JwtBearerOptions : AuthenticationSchemeOptions
    {
        public TokenValidationParameters TokenValidationParameters { get; set; } = new();
        public bool RequireHttpsMetadata { get; set; }
        public string? Authority { get; set; }
        public string? Audience { get; set; }
        public bool SaveToken { get; set; }
        public JwtBearerEvents Events { get; set; } = new();
    }

    /// <summary>
    /// Declared so application code that assigns these compiles. The real
    /// package invokes them; this does not, and no Loco code sets them.
    /// </summary>
    public class JwtBearerEvents
    {
        public Func<object, Task> OnAuthenticationFailed { get; set; } = _ => Task.CompletedTask;
        public Func<object, Task> OnTokenValidated { get; set; } = _ => Task.CompletedTask;
        public Func<object, Task> OnChallenge { get; set; } = _ => Task.CompletedTask;
    }

    public class JwtBearerHandler : AuthenticationHandler<JwtBearerOptions>
    {
        public JwtBearerHandler(
            IOptionsMonitor<JwtBearerOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? header = Request.Headers.Authorization;

            // No credentials is not a failure: it leaves the request anonymous
            // so the authorization policy decides, which is what produces a 401
            // on a protected endpoint and a 200 on an anonymous one.
            if (string.IsNullOrWhiteSpace(header))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            const string prefix = "Bearer ";
            if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.Fail("Not a bearer token"));
            }

            var token = header[prefix.Length..].Trim();

            try
            {
                // Microsoft's validator, against the parameters the application
                // configured. Signature, issuer, audience and lifetime are all
                // checked by it, not by anything here.
                var principal = new JwtSecurityTokenHandler()
                    .ValidateToken(token, Options.TokenValidationParameters, out _);

                var ticket = new AuthenticationTicket(principal, Scheme.Name);
                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (Exception ex)
            {
                return Task.FromResult(AuthenticateResult.Fail(ex));
            }
        }

        /// <summary>
        /// The real package answers an unauthenticated request with
        /// 401 + WWW-Authenticate. Reproduced because tests assert the status.
        /// </summary>
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.Headers.WWWAuthenticate = "Bearer";
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }

    public static class JwtBearerExtensions
    {
        public static AuthenticationBuilder AddJwtBearer(this AuthenticationBuilder builder) =>
            builder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

        public static AuthenticationBuilder AddJwtBearer(
            this AuthenticationBuilder builder, Action<JwtBearerOptions> configure) =>
            builder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, configure);

        public static AuthenticationBuilder AddJwtBearer(
            this AuthenticationBuilder builder, string scheme, Action<JwtBearerOptions> configure) =>
            builder.AddScheme<JwtBearerOptions, JwtBearerHandler>(scheme, configure);
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
