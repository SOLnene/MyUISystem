using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 在 Unity 项目的预定义程序集里，找到所有实现某个接口（比如 IEvent）的类型。
/// </summary>
public static class PredefinedAssemblyUtil
{
    enum  AssemblyType
    {
        AssemblyCSharp,
        AssemblyCSharpEditor,
        AssemblyCSharpEditorFirstPass,
        AssemblyCSharpFirstPass,
    }
    //在 C# 里，? 表示 可空类型
    /// <summary>
    /// 将程序集名称映射到相应的 AssemblyType
    /// </summary>
    /// <returns>AssemblyCSharp和AssemblyCSharpFirstPass中的</returns>
    static AssemblyType? GetAssemblyType(string assemblyName)
    {
        return assemblyName switch
        {
            "Assembly-CSharp" => AssemblyType.AssemblyCSharp,
            "Assembly-CSharp-Editor" => AssemblyType.AssemblyCSharpEditor,
            "Assembly-CSharp-Editor-firstpass" => AssemblyType.AssemblyCSharpEditorFirstPass,
            "Assembly-CSharp-firstpass" => AssemblyType.AssemblyCSharpFirstPass,
            _ => null
        };
    }
    
    /// <summary>
    /// 筛选程序集中的某个接口的所有实现类，添加到结果集合里。
    /// </summary>
    /// <param name="assemblyTypes">某个程序集里的所有类型（Type[] 数组）</param>
    /// <param name="interfaceType">目标接口的类型，例如 typeof(IEvent)</param>
    /// <param name="results">用于保存筛选出来的结果集合</param>
    static void AddTypesFromAssembly(Type[] assemblyTypes, Type interfaceType, ICollection<Type> results)
    {
        if (assemblyTypes == null)
        {
            return;
        }
        for (int i = 0; i < assemblyTypes.Length; i++)
        {
            Type type = assemblyTypes[i];
            if (type != interfaceType && interfaceType.IsAssignableFrom(type))
            {
                results.Add(type);
            }
        }
    }
    
    public static List<Type> GetTypes(Type interfaceType)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Dictionary<AssemblyType, Type[]> assemblyTypes = new Dictionary<AssemblyType, Type[]>();
        List<Type> types = new List<Type>();
        for (int i = 0; i < assemblies.Length; i++)
        {
            AssemblyType? assemblyType = GetAssemblyType(assemblies[i].GetName().Name);
            if (assemblyType != null)
            {
                assemblyTypes.Add((AssemblyType)assemblyType,assemblies[i].GetTypes());
            }
        }
        assemblyTypes.TryGetValue(AssemblyType.AssemblyCSharp, out var assemblyCSharpTypes);
        AddTypesFromAssembly(assemblyCSharpTypes, interfaceType, types);

        assemblyTypes.TryGetValue(AssemblyType.AssemblyCSharpFirstPass, out var assemblyCSharpFirstPassTypes);
        AddTypesFromAssembly(assemblyCSharpFirstPassTypes, interfaceType, types);
        return types;
    }
}
