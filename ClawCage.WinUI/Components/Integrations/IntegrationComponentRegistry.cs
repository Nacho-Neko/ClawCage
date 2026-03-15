using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ClawCage.WinUI.Components.Integrations
{
    internal static class IntegrationComponentRegistry
    {
        private static readonly Dictionary<string, IIntegrationWizardComponent> Components = new(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        internal static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;

            var componentTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IIntegrationWizardComponent).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && !t.IsInterface
                            && t.GetConstructor(Type.EmptyTypes) is not null);

            foreach (var type in componentTypes)
            {
                if (Activator.CreateInstance(type) is IIntegrationWizardComponent component)
                    Register(component);
            }
        }

        internal static void Register(IIntegrationWizardComponent component)
        {
            Components[component.Key] = component;
        }

        internal static IReadOnlyList<IIntegrationWizardComponent> GetAll()
            => Components.Values.ToList();

        internal static bool TryGet(string key, out IIntegrationWizardComponent? component)
            => Components.TryGetValue(key, out component);
    }
}
