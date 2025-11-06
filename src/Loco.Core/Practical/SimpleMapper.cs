// Robert C. Martin: "Clean code is simple and direct"
// John Carmack: "Avoid premature abstraction"

using System.Linq.Expressions;
using System.Reflection;

namespace Loco.Core.Practical;

/// <summary>
/// Simple object mapper - Map between objects without heavy frameworks
/// Fast, type-safe, convention-based
/// </summary>
public class SimpleMapper
{
    private readonly Dictionary<(Type, Type), Delegate> _mappings = new();

    // Map object to another type
    public TDest Map<TSource, TDest>(TSource source) where TDest : new()
    {
        if (source == null) return default!;

        var key = (typeof(TSource), typeof(TDest));

        if (_mappings.TryGetValue(key, out var mapping))
        {
            return ((Func<TSource, TDest>)mapping)(source);
        }

        // Auto-map by convention
        return AutoMap<TSource, TDest>(source);
    }

    // Map collection
    public List<TDest> MapList<TSource, TDest>(IEnumerable<TSource> sources) where TDest : new()
    {
        return sources.Select(s => Map<TSource, TDest>(s)).ToList();
    }

    // Configure custom mapping
    public void CreateMap<TSource, TDest>(Func<TSource, TDest> mapping)
    {
        var key = (typeof(TSource), typeof(TDest));
        _mappings[key] = mapping;
    }

    // Auto-map by matching property names
    private TDest AutoMap<TSource, TDest>(TSource source) where TDest : new()
    {
        var dest = new TDest();
        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destProps = typeof(TDest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        foreach (var sourceProp in sourceProps)
        {
            if (destProps.TryGetValue(sourceProp.Name, out var destProp))
            {
                if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                {
                    var value = sourceProp.GetValue(source);
                    destProp.SetValue(dest, value);
                }
                else
                {
                    // Try convert
                    try
                    {
                        var value = sourceProp.GetValue(source);
                        if (value != null)
                        {
                            var converted = Convert.ChangeType(value, destProp.PropertyType);
                            destProp.SetValue(dest, converted);
                        }
                    }
                    catch { }
                }
            }
        }

        return dest;
    }

    // Merge source properties into existing destination
    public void Merge<TSource, TDest>(TSource source, TDest dest)
    {
        if (source == null || dest == null) return;

        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destProps = typeof(TDest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name, p => p);

        foreach (var sourceProp in sourceProps)
        {
            if (destProps.TryGetValue(sourceProp.Name, out var destProp))
            {
                var value = sourceProp.GetValue(source);
                if (value != null)
                {
                    try
                    {
                        if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                        {
                            destProp.SetValue(dest, value);
                        }
                        else
                        {
                            var converted = Convert.ChangeType(value, destProp.PropertyType);
                            destProp.SetValue(dest, converted);
                        }
                    }
                    catch { }
                }
            }
        }
    }
}

/// <summary>
/// Fluent mapper builder
/// </summary>
public class MapperBuilder<TSource, TDest> where TDest : new()
{
    private readonly List<Action<TSource, TDest>> _mappings = new();

    public MapperBuilder<TSource, TDest> ForMember<TProp>(
        Expression<Func<TDest, TProp>> destMember,
        Func<TSource, TProp> mapFrom)
    {
        _mappings.Add((source, dest) =>
        {
            var memberExpr = (MemberExpression)destMember.Body;
            var prop = (PropertyInfo)memberExpr.Member;
            var value = mapFrom(source);
            prop.SetValue(dest, value);
        });
        return this;
    }

    public Func<TSource, TDest> Build()
    {
        return source =>
        {
            var dest = new TDest();
            foreach (var mapping in _mappings)
            {
                mapping(source, dest);
            }
            return dest;
        };
    }
}

/// <summary>
/// Simple DTO mapper with attributes
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class MapFromAttribute : Attribute
{
    public string SourceProperty { get; }

    public MapFromAttribute(string sourceProperty)
    {
        SourceProperty = sourceProperty;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class IgnoreMapAttribute : Attribute { }

public static class AttributeMapper
{
    public static TDest Map<TSource, TDest>(TSource source) where TDest : new()
    {
        if (source == null) return default!;

        var dest = new TDest();
        var destProps = typeof(TDest).GetProperties()
            .Where(p => p.CanWrite && !p.GetCustomAttributes<IgnoreMapAttribute>().Any());

        foreach (var destProp in destProps)
        {
            var mapFrom = destProp.GetCustomAttribute<MapFromAttribute>();
            var sourcePropName = mapFrom?.SourceProperty ?? destProp.Name;

            var sourceProp = typeof(TSource).GetProperty(sourcePropName);
            if (sourceProp != null)
            {
                var value = sourceProp.GetValue(source);
                if (value != null)
                {
                    try
                    {
                        if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                        {
                            destProp.SetValue(dest, value);
                        }
                        else
                        {
                            var converted = Convert.ChangeType(value, destProp.PropertyType);
                            destProp.SetValue(dest, converted);
                        }
                    }
                    catch { }
                }
            }
        }

        return dest;
    }
}

/// <summary>
/// Example domain and DTO classes
/// </summary>
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "";
}

public class UserViewModel
{
    public int UserId { get; set; }

    [MapFrom("FirstName")]
    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    [IgnoreMap]
    public string InternalField { get; set; } = "";
}

/// <summary>
/// Example usage
/// </summary>
public class MapperExamples
{
    public static void Examples()
    {
        var user = new User
        {
            Id = 1,
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        // Simple auto-mapping
        var mapper = new SimpleMapper();
        mapper.CreateMap<User, UserDto>(u => new UserDto
        {
            Id = u.Id,
            FullName = $"{u.FirstName} {u.LastName}",
            Email = u.Email,
            Status = u.IsActive ? "Active" : "Inactive"
        });

        var dto = mapper.Map<User, UserDto>(user);

        // Fluent mapping
        var fluentMapper = new MapperBuilder<User, UserDto>()
            .ForMember(d => d.Id, s => s.Id)
            .ForMember(d => d.FullName, s => $"{s.FirstName} {s.LastName}")
            .ForMember(d => d.Email, s => s.Email)
            .ForMember(d => d.Status, s => s.IsActive ? "Active" : "Inactive")
            .Build();

        var dto2 = fluentMapper(user);

        // Attribute-based mapping
        var viewModel = AttributeMapper.Map<User, UserViewModel>(user);

        // Map collections
        var users = new List<User> { user };
        var dtos = mapper.MapList<User, UserDto>(users);

        // Merge objects
        var existingDto = new UserDto { Id = 1, FullName = "Old Name" };
        mapper.Merge(user, existingDto);
    }
}

/// <summary>
/// Projection mapper for queries
/// </summary>
public static class ProjectionMapper
{
    public static Expression<Func<TSource, TDest>> CreateMapExpression<TSource, TDest>()
        where TDest : new()
    {
        var sourceParam = Expression.Parameter(typeof(TSource), "source");
        var bindings = new List<MemberBinding>();

        var destProps = typeof(TDest).GetProperties().Where(p => p.CanWrite);
        var sourceProps = typeof(TSource).GetProperties().ToDictionary(p => p.Name);

        foreach (var destProp in destProps)
        {
            if (sourceProps.TryGetValue(destProp.Name, out var sourceProp))
            {
                if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                {
                    var sourceMember = Expression.Property(sourceParam, sourceProp);
                    bindings.Add(Expression.Bind(destProp, sourceMember));
                }
            }
        }

        var newExpr = Expression.New(typeof(TDest));
        var memberInit = Expression.MemberInit(newExpr, bindings);

        return Expression.Lambda<Func<TSource, TDest>>(memberInit, sourceParam);
    }
}

/// <summary>
/// Dictionary mapper
/// </summary>
public static class DictionaryMapper
{
    public static Dictionary<string, object?> ToDict<T>(T obj)
    {
        var dict = new Dictionary<string, object?>();
        var props = typeof(T).GetProperties();

        foreach (var prop in props)
        {
            dict[prop.Name] = prop.GetValue(obj);
        }

        return dict;
    }

    public static T FromDict<T>(Dictionary<string, object?> dict) where T : new()
    {
        var obj = new T();
        var props = typeof(T).GetProperties().Where(p => p.CanWrite);

        foreach (var prop in props)
        {
            if (dict.TryGetValue(prop.Name, out var value) && value != null)
            {
                try
                {
                    if (prop.PropertyType.IsAssignableFrom(value.GetType()))
                    {
                        prop.SetValue(obj, value);
                    }
                    else
                    {
                        var converted = Convert.ChangeType(value, prop.PropertyType);
                        prop.SetValue(obj, converted);
                    }
                }
                catch { }
            }
        }

        return obj;
    }
}