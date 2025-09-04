using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using NAudio.Wave;
using System;

public class WebPlayMusic : MonoBehaviour
{
    public AudioSource audioSource;

    public static WebPlayMusic webPlayMusic;

    void Start()
    {
        webPlayMusic = this;
    }

    public static void PlayMusic(string file)
    {
        //播放音乐
        AudioClip audioClip = webPlayMusic.LoadAudio(file);
        webPlayMusic.audioSource.clip = audioClip;
        webPlayMusic.audioSource.Play();
    }
    public static void StopMusic()
    {
        webPlayMusic.audioSource.Stop();
    }

    // 使用 NAudio 加载 WAV 文件
    AudioClip LoadAudio(string filePath)
    {
        if (File.Exists(filePath))
        {
            using (var reader = new WaveFileReader(filePath))
            {
                // 获取音频文件的采样频率和通道数
                int sampleRate = reader.WaveFormat.SampleRate;
                int channels = reader.WaveFormat.Channels;
                int sampleCount = (int)(reader.Length / 2 / channels); // 16-bit samples

                // 创建一个 AudioClip
                AudioClip audioClip = AudioClip.Create("name", sampleCount, channels, sampleRate, false);

                // 读取音频数据
                float[] audioData = new float[sampleCount * channels];
                byte[] buffer = new byte[sampleCount * channels * 2]; // 16-bit samples
                int bytesRead = reader.Read(buffer, 0, buffer.Length);
                for (int i = 0; i < bytesRead / 2; i++)
                {
                    audioData[i] = (short)(buffer[i * 2] | buffer[i * 2 + 1] << 8) / 32768.0f; // Normalize to [-1, 1]
                }

                // 设置 AudioClip 数据
                audioClip.SetData(audioData, 0);
                return audioClip;
            }
        }
        return null;
    }
}
