using System;
using System.Collections.Generic;
using System.Reflection;

namespace DotNetPatcher {
    public class GetOnce {
        public static bool Log = false;
        public interface IStaticPropertyInitializer {
            void AfterInitialization(PropertyInfo propertyInfo);
        }
        public class Gen<T, I> {
            public static PropertyInfo PropertyInfo;
            public static object Backup;
            public static MethodInfo MethodInfo = null;

            public static T GetValue() {
                if (Backup == null) {
                    if (Log) Console.WriteLine($"{PropertyInfo.DeclaringType.Name}.{PropertyInfo.Name} Call original getter.");
                    Backup = MethodInfo.Invoke(null, new object[] { });
                    if (Backup != null) {
                        if (Backup is IStaticPropertyInitializer) {
                            (Backup as IStaticPropertyInitializer).AfterInitialization(PropertyInfo);
                        }
                    }
                }

                if (Log) Console.WriteLine($"{PropertyInfo.DeclaringType.Name}.{PropertyInfo.Name} Use previously returned value.");
                return (T)Backup;
            }
        }

        private static Dictionary<Type, Type> currentType = new Dictionary<Type, Type>();

        public static Type GetNewType(Type propertyType) {
            if (!currentType.ContainsKey(propertyType)) {
                currentType.Add(propertyType, typeof(int));
            }
            var lastType = currentType[propertyType];
            var result = typeof(Gen<,>).MakeGenericType(propertyType, lastType);
            currentType[propertyType] = result;
            return result;
        }

        public static void WrapPropertyGetter(PropertyInfo propertyInfo) {
            var wrapper = GetNewType(propertyInfo.PropertyType);
            wrapper.GetField("PropertyInfo", BindingFlags.Static | BindingFlags.Public).SetValue(null, propertyInfo);
            var a = wrapper.GetMethod("GetValue", BindingFlags.Static | BindingFlags.Public);
            var b = propertyInfo.GetMethod;
            MethodEditor.SwapMethods(a, b);
            wrapper.GetField("MethodInfo", BindingFlags.Static | BindingFlags.Public).SetValue(null, a);
        }
    }
}

