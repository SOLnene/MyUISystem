#if UNITY_EDITOR
using System.Text;
using UnityEngine;

namespace SkierFramework
{
    public static class UIBindingCodeGenerator
    {
        public static void CopyCSharpToClipboard(UIControlData controlData, string accessLevel)
        {
            // 调用保存资源会导致 prefab 发生变化，因此只有有需要时才保存
            if (IsNeedSave(controlData))
                UIBindingPrefabSaveHelper.SavePrefab(controlData.gameObject);

            StringBuilder sb = new StringBuilder(1024);
            sb.AppendLine("#region 控件绑定变量声明，自动生成请勿手改");
            sb.AppendLine("\t\t#pragma warning disable 0649"); // 变量未赋值

            foreach (var ctrl in controlData.ctrlItemDatas)
            {
                if (ctrl.targets.Length == 0)
                    continue;

                if (ctrl.targets.Length == 1)
                    sb.AppendFormat("\t\t[ControlBinding]\r\n\t\t{0} {1} {2};\r\n", accessLevel, ctrl.type, ctrl.name);
                else
                    sb.AppendFormat("\t\t[ControlBinding]\r\n\t\t{0} {1}[] {2};\r\n", accessLevel, ctrl.type, ctrl.name);
            }

            sb.AppendLine();
            foreach(var subUI in controlData.subUIItemDatas)
            {
                sb.AppendFormat("\t\t[SubUIBinding]\r\n\t\t{0} UIControlData {1};\r\n", accessLevel, subUI.name);
            }
            sb.AppendLine("\t\t#pragma warning restore 0649");
            sb.Append("#endregion\r\n\r\n");

            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        public static void CopyLuaToClipboard(UIControlData controlData)
        {
            // 调用保存资源会导致 prefab 发生变化，因此只有有需要时才保存
            if (IsNeedSave(controlData))
                UIBindingPrefabSaveHelper.SavePrefab(controlData.gameObject);

            StringBuilder sb = new StringBuilder(1024);
            sb.Append("-- 控件绑定变量声明，自动生成请勿手改\r\n");

            foreach (var ctrl in controlData.ctrlItemDatas)
            {
                if (ctrl.targets.Length == 0)
                    continue;

                sb.AppendFormat("local {0}\r\n", ctrl.name);
            }

            sb.AppendFormat("\r\n");
            sb.AppendFormat("-- SubUI\r\n");
            foreach (var subUI in controlData.subUIItemDatas)
            {
                sb.AppendFormat("local {0}\r\n", subUI.name);
            }
            sb.Append("-- 控件绑定定义结束\r\n\r\n");

            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        private static bool IsNeedSave(UIControlData controlData)
        {
            foreach(var ctrl in controlData.ctrlItemDatas)
            {
                if (string.IsNullOrEmpty(ctrl.type))
                    return true;
            }
            return false;
        }
    }
}
#endif
