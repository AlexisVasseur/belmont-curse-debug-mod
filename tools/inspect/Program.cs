using System.Reflection;
using System.Runtime.Loader;
// Dossier du jeu : variable d'environnement BELMONT_GAMEDIR, sinon le chemin par défaut.
var gameDir = Environment.GetEnvironmentVariable("BELMONT_GAMEDIR") ?? @"C:\Games\Castlevania Belmont's Curse (Modded)";
var dirs = new[] { Path.Combine(gameDir, "BepInEx", "interop"), Path.Combine(gameDir, "BepInEx", "core") };
AssemblyLoadContext.Default.Resolving += (ctx, name) => { foreach (var d in dirs) { var f = Path.Combine(d, name.Name + ".dll"); if (File.Exists(f)) return ctx.LoadFromAssemblyPath(f); } return null; };
var a = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(dirs[0], args[0] + ".dll"));
Type[] types; try { types = a.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
var rx = new System.Text.RegularExpressions.Regex(args[1], System.Text.RegularExpressions.RegexOptions.IgnoreCase);
bool members = args.Length > 2 && args[2] == "m";
foreach (var t in types)
{
    try
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        if (members)
        {
            if (!rx.IsMatch(t.FullName ?? "")) continue; if (t.IsEnum) Console.WriteLine("   enum: " + string.Join(", ", Enum.GetNames(t)));
            Console.WriteLine($"== {t.FullName} : {t.BaseType?.FullName ?? t.BaseType?.Name}");
            foreach (var p in t.GetProperties(flags)) Console.WriteLine($"   P{(p.CanWrite ? "w" : "r")} {p.PropertyType.Name}{(p.PropertyType.IsGenericType ? "<" + string.Join(",", p.PropertyType.GetGenericArguments().Select(g => g.FullName)) + ">" : "")} {p.Name}");
            if (!t.IsEnum) foreach (var f in t.GetFields(flags)) Console.WriteLine($"   F {f.FieldType.Name} {f.Name}");
            foreach (var m in t.GetMethods(flags)) if (!m.IsSpecialName) Console.WriteLine($"   M {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name))})");
        }
        else
        {
            var hits = t.GetProperties(flags).Where(p => rx.IsMatch(p.Name)).Select(p => p.Name).ToList();
            if (hits.Count > 0) Console.WriteLine($"{t.FullName}: {string.Join(", ", hits)}");
        }
    }
    catch { }
}


