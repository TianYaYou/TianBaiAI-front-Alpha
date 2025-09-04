using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MainSettingsUi
{
    public class BackGroundLog : MonoBehaviour
    {
        public int index = 0;
        public void Yes()
        {

            if (index == 0)
            {
                WebApi.Upmassage(@"{""control_object"":""endprocess"",""control_value"":0}", "pythonsystemio");
                //3秒后重启
                StartCoroutine(WaitAndRestart(3));
            }
            IEnumerator WaitAndRestart(int waitTime)
            {
                for (int i = 0; i < waitTime; i++)
                {

                    WebApi.Upmassage(@"{""control_object"":""endprocess"",""control_value"":0}", "pythonsystemio");
                    yield return new WaitForSeconds(1f);
                }
                Start();
                gameObject.SetActive(false);
            }
            void Start()
            {
                string path = MainPreference.BackGroundProjectPath + @"\run.bat";
                Debug.Log("执行路径：" + path);
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = "cmd.exe";          // 指定要运行的可执行文件
                startInfo.Arguments = $"/k \"{path}\"";  // 指定要执行的命令
                startInfo.UseShellExecute = false;  // 不使用操作系统外壳程序启动
                // 如果您希望看到输出在弹出的窗口中，通常不需要重定向标准输出和错误
                startInfo.RedirectStandardOutput = false;
                startInfo.RedirectStandardError = false;
                startInfo.CreateNoWindow = false;
                startInfo.WorkingDirectory = MainPreference.BackGroundProjectPath; // 设置工作目录
                System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
            }
        }
        public void No()
        {
            gameObject.SetActive(false);
        }
    }
}
