﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ReferenceBindTool.Editor
{
    public static class ReferenceBindUtility
    {
        
        private static readonly string[] s_AssemblyNames =
        {
#if UNITY_2017_3_OR_NEWER
            //asmdef
#endif
            "Assembly-CSharp"
        };

        /// <summary>
        /// 获取指定基类在指定程序集中的所有子类名称
        /// </summary>
        public static string[] GetTypeNames()
        {
            List<string> typeNames = new List<string>();
            foreach (string assemblyName in s_AssemblyNames)
            {
                Assembly assembly = null;
                try
                {
                    assembly = Assembly.Load(assemblyName);
                }
                catch
                {
                    continue;
                }

                if (assembly == null)
                {
                    continue;
                }

                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if (type.IsClass && !type.IsAbstract && typeof(IAutoBindRuleHelper).IsAssignableFrom(type))
                    {
                        typeNames.Add(type.FullName);
                    }
                }
            }

            typeNames.Sort();
            return typeNames.ToArray();
        }
        /// <summary>
        /// 检查引用是否可以添加
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static bool CheckIsCanAdd(UnityEngine.Object obj)
        {
            bool isFolder = obj is DefaultAsset && ProjectWindowUtil.IsFolder(obj.GetInstanceID());
            return !isFolder;
        }

        /// <summary>
        /// 获取字段名称
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static string GetFiledName(UnityEngine.Object target)
        {
            string filedName = $"{target.GetType().Name}_{target.name}".Replace(' ', '_');
            return filedName;
        }
        
        /// <summary>
        /// 获取绑定的组件使用到的命名空间
        /// </summary>
        /// <param name="target">组件绑定工具</param>
        /// <returns>绑定的组件使用到的命名空间</returns>
        private static List<string> GetNameSpaces(ReferenceBindComponent target)
        {
            List<string> nameSpaces = new List<string>
            {
                nameof(UnityEngine),
                typeof(ReferenceBindComponent).Namespace
            };
            foreach (var bindCom in target.BindObjects)
            {
                nameSpaces.Add(bindCom.GetType().Namespace);
            }
     
            return nameSpaces.Distinct().ToList();
        }
        /// <summary>
        /// 生成自动绑定代码
        /// </summary>
        public static string GenAutoBindCode(ReferenceBindComponent target, string className)
        {
            target.Refresh();
            if (target.BindAssetsOrPrefabs.Find(_ =>  _.IsRepeatName) != null ||
                target.BindComponents.Find(_ =>  _.IsRepeatName) != null)
            {
                throw new Exception("绑定对象中存在同名,请修改后重新生成。");
            }
            StringBuilder stringBuilder = new StringBuilder(2048);

            List<string> nameSpaces = GetNameSpaces(target);
            foreach (var nameSpace in nameSpaces)
            {
                stringBuilder.AppendLine($"using {nameSpace};");
            }
            stringBuilder.AppendLine("");

            string indentation = string.Empty;
            if (!string.IsNullOrEmpty(target.SettingData.Namespace))
            {
                //命名空间
                stringBuilder.AppendLine("namespace " + target.SettingData.Namespace);
                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine("");
                indentation = "\t";
            }

            //类名
            stringBuilder.AppendLine($"{indentation}public partial class {className}");
            stringBuilder.AppendLine($"{indentation}{{");
            stringBuilder.AppendLine("");

            
            List<ReferenceBindComponent.BindObjectData> allBindObjectDataList = new List<ReferenceBindComponent.BindObjectData>(target.GetAllBindObjectsCount());
            allBindObjectDataList.AddRange(target.BindAssetsOrPrefabs);
            allBindObjectDataList.AddRange(target.BindComponents);
            //组件字段
            foreach (var data in allBindObjectDataList)
            {
                stringBuilder.AppendLine($"{indentation}\tprivate {data.BindObject.GetType().Name} m_{data.FieldName};");
            }

            stringBuilder.AppendLine("");

            stringBuilder.AppendLine($"{indentation}\tprivate void GetBindObjects(GameObject go)");
            stringBuilder.AppendLine($"{indentation}\t{{");

      
            stringBuilder.AppendLine($"{indentation}\t\t{nameof(ReferenceBindComponent)} bindComponent = go.GetComponent<{nameof(ReferenceBindComponent)}>();");
            stringBuilder.AppendLine("");

            //根据索引获取
            
            for (int i = 0; i < allBindObjectDataList.Count; i++)
            {
                ReferenceBindComponent.BindObjectData data = allBindObjectDataList[i];
                stringBuilder.AppendLine($"{indentation}\t\tm_{data.FieldName} = bindComponent.GetBindObject<{data.BindObject.GetType().Name}>({i});");
            }

            stringBuilder.AppendLine($"{indentation}\t}}");

            stringBuilder.AppendLine($"{indentation}}}");

            if (!string.IsNullOrEmpty(target.SettingData.Namespace))
            {
                stringBuilder.AppendLine("}");
            }

            return stringBuilder.ToString();
        }


        /// <summary>
        /// 生成自动绑定代码
        /// </summary>
        public static bool GenAutoBindCode(ReferenceBindComponent target, string className, string codeFolderPath)
        {
            if (!Directory.Exists(codeFolderPath))
            {
                Debug.LogError($"{target.gameObject.name}的代码保存路径{codeFolderPath}无效");
                return false;
            }
            string str = GenAutoBindCode(target, className);
            string filePath = $"{codeFolderPath}/{className}.BindComponents.cs";
            if (File.Exists(filePath) && str == File.ReadAllText(filePath))
            {
                Debug.Log("文件内容相同。不需要重新生成。");
                return true;
            }
          
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.Write(str);
            }

            AssetDatabase.Refresh();
            Debug.Log($"代码生成成功,生成路径: {codeFolderPath}/{className}.BindComponents.cs");

            return true;
        }
    }
}