using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using Project_Star.Core.Interfaces;

namespace Project_Star.Core.Pool;

// 反射模板池基类（全局层）：用反射扫描程序集收集所有非抽象实体子类各建一个作模板；创建时复制独立实例。
public abstract class PoolBase<T> where T : RefCounted, IEntity
{
    private readonly List<T> _templates = new();

    protected PoolBase()
    {
        CollectTemplates();
    }

    // 模板列表（每个非抽象子类各一个）
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

    // 从模板复制独立实例（供选择/创建实体时调用）
    public T CreateFromTemplate(T template) => (T)template.Clone();
}