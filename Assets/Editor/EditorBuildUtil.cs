using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

// EditorBuildUtil.cs —— 编辑器构建公共工具：路径、类型查找、文件与目录操作
namespace Gunplay.EditorTools
{
    /// <summary>编辑器构建公共工具（仅供 Assets/Editor 下的脚本使用）</summary>
    internal static class EditorBuildUtil
    {
        /// <summary>工程根目录（Assets 的上一级）</summary>
        internal static string ProjectRoot
        {
            get
            {
                DirectoryInfo parent = Directory.GetParent(Application.dataPath);
                return parent != null ? parent.FullName : Application.dataPath;
            }
        }

        /// <summary>把工程内相对路径转换为绝对路径</summary>
        internal static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot, relativePath));
        }

        /// <summary>确保目录存在（父目录会一并创建）</summary>
        internal static void EnsureDirectory(string directory)
        {
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>安全获取程序集内全部类型（忽略加载失败的引用类型）</summary>
        internal static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                List<Type> loaded = new List<Type>();
                if (exception.Types != null)
                {
                    for (int i = 0; i < exception.Types.Length; i++)
                    {
                        Type type = exception.Types[i];
                        if (type != null)
                        {
                            loaded.Add(type);
                        }
                    }
                }

                return loaded;
            }
            catch (Exception)
            {
                return new Type[0];
            }
        }

        /// <summary>按全名或短名查找类型（跨程序集；找不到返回 null）</summary>
        internal static Type FindType(params string[] typeNames)
        {
            if (typeNames == null || typeNames.Length == 0)
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                foreach (Type type in SafeGetTypes(assemblies[a]))
                {
                    if (type == null)
                    {
                        continue;
                    }

                    string fullName = type.FullName;
                    string shortName = type.Name;
                    for (int n = 0; n < typeNames.Length; n++)
                    {
                        string wanted = typeNames[n];
                        if (string.IsNullOrEmpty(wanted))
                        {
                            continue;
                        }

                        if (string.Equals(fullName, wanted, StringComparison.Ordinal) ||
                            string.Equals(shortName, wanted, StringComparison.Ordinal))
                        {
                            return type;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>写 UTF-8 文本文件（无 BOM，自动创建所在目录）</summary>
        internal static void WriteAllText(string absolutePath, string content)
        {
            string directory = Path.GetDirectoryName(absolutePath);
            EnsureDirectory(directory);
            File.WriteAllText(absolutePath, content ?? string.Empty, new UTF8Encoding(false));
        }

        /// <summary>递归复制目录（目标已存在时覆盖同名文件）</summary>
        internal static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new DirectoryNotFoundException("源目录不存在: " + sourceDirectory);
            }

            EnsureDirectory(targetDirectory);
            string[] files = Directory.GetFiles(sourceDirectory);
            for (int i = 0; i < files.Length; i++)
            {
                string targetFile = Path.Combine(targetDirectory, Path.GetFileName(files[i]));
                File.Copy(files[i], targetFile, true);
            }

            string[] directories = Directory.GetDirectories(sourceDirectory);
            for (int i = 0; i < directories.Length; i++)
            {
                CopyDirectory(directories[i], Path.Combine(targetDirectory, Path.GetFileName(directories[i])));
            }
        }

        /// <summary>删除目录（不存在时忽略）</summary>
        internal static void DeleteDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>判断两个路径是否指向同一位置</summary>
        internal static bool IsSamePath(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            string first = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string second = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>把字符串转义为可放进 JSON 双引号内的值</summary>
        internal static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(value.Length + 16);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
