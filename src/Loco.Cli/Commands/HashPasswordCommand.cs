using System;
using System.Threading.Tasks;
using Loco.Core.Security;

namespace Loco.Cli.Commands;

/// <summary>
/// Produces the PBKDF2 hash that Auth:Users entries hold.
///
/// Without this, nobody could start using Loco at all. Every API controller
/// carries [Authorize]; the token endpoint refuses to issue anything when
/// Auth:Users is empty (it fails closed rather than accepting all); and the
/// only way to fill Auth:Users is with a hash in PasswordHasher's format.
/// PasswordHasher lived inside Loco.Api, reachable from no command and no
/// endpoint - so the documented first step, "add a user with a PBKDF2-hashed
/// password", had no way to be carried out.
///
/// The password is read from stdin, never from argv: an argument would be
/// recorded in shell history and visible in the process list to every other
/// user on the machine. Piping works for scripts:
///
///     printf 'my-password' | loco hash-password
/// </summary>
public class HashPasswordCommand : BaseCommand
{
    public override CommandHelp GetHelp() => new()
    {
        Name = "hash-password",
        Description = "Hash a password for an Auth:Users entry (reads stdin)",
        Usage = "loco hash-password",
        Examples = new[]
        {
            "printf 'my-password' | loco hash-password",
            "loco hash-password   # prompts, then prints the hash",
        },
    };

    public override Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: hash-password takes no arguments.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("The password is read from standard input, so it does not appear");
            Console.WriteLine("in your shell history or in the process list:");
            Console.WriteLine();
            Console.WriteLine("    printf 'my-password' | loco hash-password");
            return Task.FromResult(1);
        }

        if (!Console.IsInputRedirected)
        {
            Console.Write("Password: ");
        }

        var password = Console.ReadLine();

        if (string.IsNullOrEmpty(password))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: no password was supplied on standard input.");
            Console.ResetColor();
            return Task.FromResult(1);
        }

        var hash = PasswordHasher.Hash(password);

        Console.WriteLine();
        Console.WriteLine(hash);
        Console.WriteLine();
        Console.WriteLine("Add it to the API's configuration, for example in appsettings.json:");
        Console.WriteLine();
        Console.WriteLine("  \"Auth\": {");
        Console.WriteLine("    \"Users\": [");
        Console.WriteLine("      {");
        Console.WriteLine("        \"Username\": \"admin\",");
        Console.WriteLine($"        \"PasswordHash\": \"{hash}\",");
        Console.WriteLine("        \"Scopes\": [ \"workflows:read\", \"workflows:manage\", \"workflows:execute\" ]");
        Console.WriteLine("      }");
        Console.WriteLine("    ]");
        Console.WriteLine("  }");

        return Task.FromResult(0);
    }
}
