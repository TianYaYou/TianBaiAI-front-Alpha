using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using System.Linq;

public class WebApi : MonoBehaviour
{
    public static WebApi webApi;

    private const string BASE_URL = "http://127.0.0.1:4070/";
    private const float POLLING_INTERVAL = 0.5f; // Unity 轮询 Python 服务器的频率

    // 在 Unity 编辑器中可见，用于演示 (可选)
    [SerializeField] private string unityMessageToSend = "你好，来自 Unity 的消息!";

    void Start()
    {
        // 开始轮询从 Python 服务器接收消息
        StartCoroutine(PollForIncomingMessages());
        webApi = this;
    }

    /// <summary>
    /// 将消息发送到 Python
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="message_type">消息类型</param>
    public static void Upmassage(string message, string message_type = "unity_message") => webApi.upmassage(message, message_type);

    /// <summary>
    /// 从你的 Unity 代码中调用此函数，将消息发送到 Python
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="message_type">消息类型</param>
    public void upmassage(string message, string message_type = "unity_message")
    {
        // 你可以在这里添加更多的消息类型
        // 例如: message_type = "test" 或 "heartbeat"
        Debug.Log($"Unity 正在发送消息: {message}");
        // 调用协程发送消息到 Python
        StartCoroutine(SendUnityMessageToPython(message, message_type));
    }


    private IEnumerator SendUnityMessageToPython(string message, string message_type)
    {
        // 创建要发送的 JSON 对象
        // Unity 的 JsonUtility 适用于简单的 JSON 结构
        // 对于更复杂的 JSON，可以考虑使用 Newtonsoft.Json (需要导入库)
        string jsonToSend = JsonUtility.ToJson(new WebApiMassage
        {
            message_type = message_type,
            time = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            message_info = message
        });

        using (UnityWebRequest request = new UnityWebRequest(BASE_URL + "upmassage", "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonToSend);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"Unity 正在发送: {jsonToSend} 到 {BASE_URL}upmassage");
            yield return request.SendWebRequest(); // 发送请求

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"发送消息到 Python 时出错: {request.error}");
            }
            else
            {
                Debug.Log($"Unity 成功发送消息。Python 响应: {request.downloadHandler.text}");
            }
        }
    }

    private IEnumerator PollForIncomingMessages()
    {
        while (true) // 持续轮询
        {
            using (UnityWebRequest request = UnityWebRequest.Get(BASE_URL + "inmassage"))
            {
                yield return request.SendWebRequest(); // 发送 GET 请求

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseJson = request.downloadHandler.text;
                    Debug.Log($"Unity 收到原始轮询响应: {responseJson}"); // 解开注释可用于调试
                    try
                    {
                        WebApiMassage apiMassage = null;
                        // 解析 JSON 响应 ,使用nunetwtonsoft.json
                        try
                        {
                            apiMassage = JsonConvert.DeserializeObject<WebApiMassage>(responseJson);
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"解析 Python 响应 JSON 时出错: {e.Message}\n响应内容: {responseJson}");
                        }

                        if (apiMassage != null)
                        {
                            // 处理接收到的消息
                            //Debug.Log($"Unity 收到来自 Python 的消息: {apiMassage.message_info}");
                            PredosingMassage(apiMassage);
                        }


                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"解析 Python 响应 JSON 时出错: {e.Message}\n响应内容: {responseJson}");
                        StatusBox.Warning($"错误的包: {request.error} \n ->> {responseJson} \n ->>?? {e.Message}");
                    }
                }
                else if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"轮询 Python 服务器时出错: {request.error}");
                    StatusBox.Error($"服务器出错: {request.error}");
                }
            }
            yield return new WaitForSeconds(POLLING_INTERVAL); // 等待一段时间再进行下一次轮询
        }
    }

    // 示例：你可以在按键事件中触发发送消息
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            upmassage(unityMessageToSend);
        }

    }


    void PredosingMassage(WebApiMassage message)
    {

        // string message_type = message.message_type;
        // string message_info = message.message_info;
        // if (message_type == "test")
        // {
        //     Debug.Log($"Unity 收到测试消息{message.time}: {message_info}");
        // }
        // else if (message_type == "heartbeat")
        // {
        //     //Debug.Log($"Unity 收到心跳消息{message.time}");
        //     StatusBox.Heartbeat();
        // }
        // else if (message_type == "showmessage")
        // {
        //     WebDialog.Dialog(message_info);
        // }
        // else
        // {
        //     // 查找所有实现了 WebApiMessageInfo 接口的非接口和抽象类
        //     IEnumerable<Type> handlerTypes = AppDomain.CurrentDomain.GetAssemblies()
        //     .SelectMany(assembly => assembly.GetTypes())
        //     .Where(type => typeof(WebApiMessageInfo).IsAssignableFrom(type));
        //     foreach (Type handlerType in handlerTypes)
        //     {
        //         try
        //         {
        //             if (handlerType is WebApiMessageInfo webApiMessageInfo && webApiMessageInfo.message_type() == message_type)
        //             {
        //                 // 反序列化存入handlerInstance
        //                 object handlerInstance = JsonConvert.DeserializeObject(message.message_info, handlerType);
        //                 if (handlerInstance is WebApiMessageInfo handler)
        //                 {
        //                     // 调用 Doing() 方法处理消息
        //                     handler.Doing();
        //                     return; // 找到匹配的处理类后即可返回
        //                 }
        //             }
        //         }
        //         catch (Exception e)
        //         {
        //             Debug.LogError($"创建或调用消息处理器 {handlerType.Name} 时出错: {e.Message}");
        //         }
        //     }

        //     // 如果没有找到匹配的处理类
        //     Debug.LogWarning($"未找到处理类型为 '{message_type}' 的消息处理器。");

        // }

        switch (message.message_type)
        {
            case "test":
                Debug.Log($"Unity 收到测试消息{message.time}: {message.message_info}");
                break;
            case "heartbeat":
                //Debug.Log($"Unity 收到心跳消息{message.time}");
                StatusBox.Heartbeat();
                break;
            case "showmessage":
                WebDialog.Dialog(message.message_info);
                break;

            case "control":
                //Debug.Log($"Unity 收到控制消息{message.time}: {message.message_info}");
                ControlMessage controlMessage = new ControlMessage(message.message_info);
                controlMessage?.Doing();
                break;
            case "dialog":
                //Debug.Log($"Unity 收到对话消息{message.time}: {message.message_info}");
                WebApiDialogResponse dialogResponse = new WebApiDialogResponse(message.message_info);
                dialogResponse?.Doing();
                break;
            case "playmusic":
                //Debug.Log($"Unity 收到播放音乐消息{message.time}: {message.message_info}");
                WebApiPlayMusic playMusic = new WebApiPlayMusic(message.message_info);
                playMusic?.Doing();
                break;
            case "setobject":
                //Debug.Log($"Unity 收到设置对象消息{message.time}: {message.message_info}");
                WebApiSetObject setObject = new WebApiSetObject(message.message_info);
                // 处理设置对象响应
                setObject?.Doing();
                break;

            default:
                //反射所有带有WebApiMessageInfo接口的类
                foreach (Type type in AppDomain.CurrentDomain.GetAssemblies()
                 .SelectMany(assembly => assembly.GetTypes())
                 .Where(type => typeof(WebApiMessageInfo).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract))
                {
                    Debug.Log($"找到实现 WebApiMessageInfo 接口的类: {type.Name}");

                }
                Debug.Log($"Unity 收到未知类型的消息: {message.message_type}");
                break;
        }
    }
}

public interface WebApiMessageInfo
{
    public void Doing();
    public string message_type();
}

/// <summary>
/// Python 服务器的响应类
/// </summary>
[System.Serializable]
public class WebApiMassage
{
    /// <summary>
    /// 服务器的响应消息类型
    /// </summary>
    public string message_type;

    /// <summary>
    /// 服务器的响应时间戳
    /// </summary>
    public string time;

    /// <summary>
    /// 服务器接收到的消息内容
    /// </summary>
    public string message_info;
}

/// <summary>
/// 处理控制类消息的类
/// </summary>
[System.Serializable]
public class ControlMessage : WebApiMessageInfo
{
    public string message_type() => "control";
    public string control_object;
    public int control_value;

    /// <summary>
    /// 构造函数，传入 JSON 字符串
    /// </summary>
    public ControlMessage(string json)
    {
        // 解析 JSON 字符串
        try
        {
            ControlMessage controlMessage = JsonConvert.DeserializeObject<ControlMessage>(json);
            control_object = controlMessage.control_object;
            control_value = controlMessage.control_value;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析控制消息时出错: {e.Message}");
        }

    }

    public void Doing()
    {
        Debug.Log($"控制对象: {control_object}, 控制值: {control_value}");
        // 在这里添加控制逻辑
        switch (control_object)
        {
            case "camera":
                // 控制相机
                break;
            case "Live2DExpressionList":
                // 控制 Live2D 模型的表情列表
                Live2DExpressionButtonCreator.SetExpression(control_value);
                break;
            case "dialoginput":
                WebDialog.GitInputDialogControl(control_value);
                /*
                    要求传递一个json以这样格式的：
                    {
                        message_type: "control",
                        "time": "2023-10-01 12:00:00",
                        "message_info": {
                            "control_object": "dialoginput",
                            "control_value": 1
                        }
                    }
                */
                break;
            case "dialogoutput":
                WebDialog.GitOutputDialogControl(control_value);
                break;
            case "SetTransparent":
                // 控制透明窗口
#if !UNITY_EDITOR
                if (control_value == 0) TransparentSetup.SetTransparent(false);
                else if (control_value == 1) TransparentSetup.SetTransparent(true);
#endif
#if UNITY_EDITOR
                Debug.Log("编辑器模式下不支持透明窗口");
#endif
                break;
            case "SetClickable":
                // 控制是否可点击
#if !UNITY_EDITOR
                if (control_value == 0) TransparentSetup.SetClickable(false);
                else if (control_value == 1) TransparentSetup.SetClickable(true);
#endif
#if UNITY_EDITOR
                Debug.Log("编辑器模式下不支持点击穿透");
#endif
                break;
            default:
                Debug.LogWarning($"未知控制对象: {control_object}");
                break;
        }
    }
}

public class WebApiSetObject : WebApiMessageInfo
{
    public string message_type() => "setobject";
    public string object_name;
    public string object_value;

    public WebApiSetObject(string json)
    {
        // 解析 JSON 字符串
        try
        {
            WebApiSetObject setObject = JsonConvert.DeserializeObject<WebApiSetObject>(json);
            object_name = setObject.object_name;
            object_value = setObject.object_value;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析设置对象消息时出错: {e.Message}");
        }
    }
    public void Doing()
    {
        Debug.Log($"控制对象: {object_name}, 控制值: {object_value}");
        // 在这里添加控制逻辑
        switch (object_name)
        {
            case "InputBackgroundText":
                /*
                    要求传递一个json以这样格式的：
                    {
                        message_type: "setobject",
                        "time": "2023-10-01 12:00:00",
                        "message_info": {
                            "object_name": "InputBackgroundText",
                            "object_value": "请输入内容"
                        }
                    }
                */
                WebDialog.SetInputBackgroundText(object_value);
                break;
            default:
                Debug.LogWarning($"未知控制对象: {object_name}");
                break;
        }
    }
}

/*
{
  "response": {
        "content": "你好，有什么我可以帮助你的吗？",
        "emotion": "高兴",
        "movement": "挥手",
        "favorability": 0.8,
        "readmemory":{
            "time": "",
            "key": "事件",
            "content_key": "小猫"
        }
        "writememory":{
            "time": "",
            "key": ["爱好","喜好","习惯"],
            "content": "墨白喜欢听音乐"
        }
        "actions": [
            "打开Vscode",
            "编写hello world",
            "关闭Vscode"
        ]
    }
}
*/
/// <summary>
/// Python 服务器的响应dialog类
/// </summary>
public class WebApiDialogResponse : WebApiMessageInfo
{
    public string message_type() => "dialog";
    public Response response;

    public WebApiDialogResponse(string json)
    {
        // 解析 JSON 字符串
        try
        {
            WebApiDialogResponse dialogResponse = JsonConvert.DeserializeObject<WebApiDialogResponse>(json);
            response = dialogResponse.response;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析对话响应时出错: {e.Message}");
        }
    }
    public class Response
    {

        public string content = "";
        public string emotion = "";
        public string movement = "";
        public float? favorability = null;
        public ReadMemory readmemory = null;
        public WriteMemory writememory = null;
        public List<string> actions = null;

    }
    public void Doing()
    {

        // 处理对话响应
        WebDialog.Dialog(response.content);
    }

    public class ReadMemory
    {
        public string time;
        public string key;
        public string content_key;
    }
    public class WriteMemory
    {
        public string time;
        public List<string> key;
        public string content;
    }
}

/// <summary>
/// Python 服务器的响应音乐类
/// </summary>
public class WebApiPlayMusic : WebApiMessageInfo
{
    public string message_type() => "playmusic";
    public string file;
    public MusicType type = MusicType.WAV;
    public PlayType play_type = PlayType.Play;
    public WebApiPlayMusic(string json)
    {
        // 解析 JSON 字符串
        try
        {
            WebApiPlayMusic playMusic = JsonConvert.DeserializeObject<WebApiPlayMusic>(json);
            file = playMusic.file;
            type = playMusic.type;
            play_type = playMusic.play_type;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析播放音乐消息时出错: {e.Message}");
        }
    }
    public void Doing()
    {

        // 处理播放音乐响应
        if (play_type == WebApiPlayMusic.PlayType.Play) WebPlayMusic.PlayMusic(file);
        else WebPlayMusic.StopMusic();
    }

    public enum MusicType
    {
        WAV,
        MP3,
        OGG
    }

    public enum PlayType
    {
        Play,
        Stop
    }
}