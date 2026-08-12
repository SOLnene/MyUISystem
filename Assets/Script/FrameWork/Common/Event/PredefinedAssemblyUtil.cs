using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// 在 Unity 项目的预定义程序集里，找到所有实现某个接口（比如 IEvent）的类型。
/// </summary>
public static class PredefinedAssemblyUtil
{
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
            if (type != null && type != interfaceType && interfaceType.IsAssignableFrom(type))
            {
                results.Add(type);
            }
        }
    }
    
    public static List<Type> GetTypes(Type interfaceType)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        List<Type> types = new List<Type>();
        foreach (Assembly assembly in assemblies)
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            Type[] assemblyTypes;
            try
            {
                assemblyTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // 某个可选依赖加载失败时，仍保留该程序集内其余可用的事件类型。
                assemblyTypes = exception.Types;
            }

            AddTypesFromAssembly(assemblyTypes, interfaceType, types);
        }

        return types;
    }
}
