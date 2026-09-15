using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Project_Star.Core.Interfaces;

namespace Project_Star.Core.Pool;

public abstract class PoolBase<T> where T : RefCounted, IEntity
{
    private readonly List<T> _templates = new();

    protected PoolBase()
    {
        CollectTemplates();
    }

    public IReadOnlyList<T> Templates => _templates;

    private void CollectTemplates()
    {
        foreach (var type in Assembly.GetAssembly(typeof(T))!.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(T).IsAssignableFrom(t)))
        {
            if (Activator.CreateInstance(type) is T instance)
                _templates.Add(instance);
        }
    }

    public T CreateFromTemplate(T template) => (T)template.Clone();
}