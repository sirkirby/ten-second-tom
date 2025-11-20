using System.Reflection;
using FluentAssertions;
using Xunit;

namespace TenSecondTom.Tests.Architecture;

/// <summary>
/// Architecture tests that enforce Vertical Slice Architecture (VSA) compliance.
/// These tests prevent regressions by failing the build when VSA principles are violated.
/// </summary>
public sealed class VsaComplianceTests
{
    private readonly Assembly _mainAssembly = typeof(TenSecondTom.Infrastructure.DependencyInjection.ServiceCollectionExtensions).Assembly;

    [Fact]
    public void Features_ShouldNotReferenceOtherFeatures()
    {
        // Arrange: Get all types in Features namespace
        var featureTypes = _mainAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("TenSecondTom.Features.") == true)
            .ToList();

        var violations = new List<string>();

        // Act: Check each feature type for references to other features
        foreach (var type in featureTypes)
        {
            var currentFeatureName = ExtractFeatureName(type.Namespace!);
            if (currentFeatureName == null) continue;

            // Get all referenced types
            var referencedTypes = GetReferencedTypes(type);

            foreach (var referencedType in referencedTypes)
            {
                var referencedFeatureName = ExtractFeatureName(referencedType.Namespace);

                // If referenced type is in a different feature, that's a violation
                if (referencedFeatureName != null &&
                    referencedFeatureName != currentFeatureName)
                {
                    violations.Add($"{type.FullName} references {referencedType.FullName} (cross-feature dependency: {currentFeatureName} → {referencedFeatureName})");
                }
            }
        }

        // Assert: No cross-feature dependencies should exist
        violations.Should().BeEmpty(
            "Features should be independent and not reference other features directly. " +
            "Use CQRS queries via MediatR or shared abstractions in Shared/ namespace instead.");
    }

    [Fact]
    public void Infrastructure_ShouldNotReferenceFeatures()
    {
        // Arrange: Get all types in Infrastructure namespace
        var infrastructureTypes = _mainAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("TenSecondTom.Infrastructure.") == true)
            .ToList();

        var violations = new List<string>();

        // Act: Check each infrastructure type for references to features
        foreach (var type in infrastructureTypes)
        {
            var referencedTypes = GetReferencedTypes(type);

            foreach (var referencedType in referencedTypes)
            {
                if (referencedType.Namespace?.StartsWith("TenSecondTom.Features.") == true)
                {
                    var featureName = ExtractFeatureName(referencedType.Namespace);
                    violations.Add($"{type.FullName} references {referencedType.FullName} (infrastructure → feature dependency)");
                }
            }
        }

        // Assert: Infrastructure should not depend on Features
        violations.Should().BeEmpty(
            "Infrastructure layer should not reference Feature layer. " +
            "Move shared abstractions to Shared/ namespace or use dependency inversion.");
    }

    [Fact]
    public void FeatureDependencyInjection_ShouldOnlyRegisterFeatureServices()
    {
        // Arrange: Get all feature DependencyInjection types
        var featureDITypes = _mainAssembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("TenSecondTom.Features.") == true)
            .Where(t => t.Name.Contains("DependencyInjection") || t.Name.Contains("Extensions"))
            .ToList();

        var violations = new List<string>();

        // Act: Check if feature DI classes reference infrastructure types
        foreach (var type in featureDITypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                var methodBody = method.GetMethodBody();
                if (methodBody == null) continue;

                // Check method signature and parameters for infrastructure types
                var parameters = method.GetParameters();
                foreach (var param in parameters)
                {
                    // Allow IServiceCollection (standard DI interface)
                    if (param.ParameterType.Namespace?.StartsWith("TenSecondTom.Infrastructure.") == true &&
                        !param.ParameterType.Name.Contains('I')) // Not an interface
                    {
                        violations.Add($"{type.FullName}.{method.Name} registers infrastructure service: {param.ParameterType.FullName}");
                    }
                }
            }
        }

        // Assert: Features should only register feature-specific services
        // Note: This is a simplified check - full validation would require IL inspection
        violations.Should().BeEmpty(
            "Feature DI registration should only register feature-specific services. " +
            "Infrastructure services belong in Infrastructure/*/DependencyInjection.cs");
    }

    [Fact]
    public void CommandBuilders_ShouldImplementICommandBuilderInterface()
    {
        // Arrange: Get all command builder types
        var commandBuilderTypes = _mainAssembly.GetTypes()
            .Where(t => t.Name.EndsWith("CommandBuilder") &&
                       t.Namespace?.StartsWith("TenSecondTom.Features.") == true)
            .ToList();

        var violations = new List<string>();

        // Act: Check if they implement ICommandBuilder
        foreach (var type in commandBuilderTypes)
        {
            var implementsICommandBuilder = type.GetInterfaces()
                .Any(i => i.Name == "ICommandBuilder");

            if (!implementsICommandBuilder && !type.Name.Contains("Config", StringComparison.Ordinal)) // Exclude IConfigSubcommandBuilder
            {
                violations.Add($"{type.FullName} does not implement ICommandBuilder interface");
            }
        }

        // Assert: All command builders should use discovery pattern
        violations.Should().BeEmpty(
            "Command builders should implement ICommandBuilder for automatic discovery. " +
            "This prevents tight coupling between CommandRegistry and feature implementations.");
    }

    /// <summary>
    /// Extracts the feature name from a namespace like "TenSecondTom.Features.Audio.Services"
    /// Returns "Audio" in this example, or null if not a feature namespace.
    /// </summary>
    private static string? ExtractFeatureName(string? ns)
    {
        if (string.IsNullOrEmpty(ns)) return null;

        const string featuresPrefix = "TenSecondTom.Features.";
        if (!ns.StartsWith(featuresPrefix)) return null;

        var remainder = ns.Substring(featuresPrefix.Length);
        var firstDot = remainder.IndexOf('.');
        return firstDot == -1 ? remainder : remainder.Substring(0, firstDot);
    }

    /// <summary>
    /// Gets all types referenced by a given type (fields, properties, method parameters, return types).
    /// </summary>
    private static HashSet<Type> GetReferencedTypes(Type type)
    {
        var referencedTypes = new HashSet<Type>();

        // Get types from fields
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            AddTypeAndGenericArguments(field.FieldType, referencedTypes);
        }

        // Get types from properties
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            AddTypeAndGenericArguments(property.PropertyType, referencedTypes);
        }

        // Get types from methods (parameters and return types)
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            AddTypeAndGenericArguments(method.ReturnType, referencedTypes);

            foreach (var param in method.GetParameters())
            {
                AddTypeAndGenericArguments(param.ParameterType, referencedTypes);
            }
        }

        // Get types from constructors
        foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            foreach (var param in constructor.GetParameters())
            {
                AddTypeAndGenericArguments(param.ParameterType, referencedTypes);
            }
        }

        return referencedTypes;
    }

    /// <summary>
    /// Adds a type and its generic arguments to the set recursively.
    /// </summary>
    private static void AddTypeAndGenericArguments(Type type, HashSet<Type> types)
    {
        if (type == null || types.Contains(type)) return;

        types.Add(type);

        // Add generic arguments recursively
        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
            {
                AddTypeAndGenericArguments(arg, types);
            }
        }

        // Add element type for arrays
        if (type.IsArray)
        {
            AddTypeAndGenericArguments(type.GetElementType()!, types);
        }
    }
}
