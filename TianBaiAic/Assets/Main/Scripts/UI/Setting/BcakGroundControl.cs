using System.Collections;
using System.Collections.Generic;
using MainSettingsUi;
using UnityEngine;

namespace MainSettingsUi
{
    public class BcakGroundControl : MonoBehaviour
    {
        public GameObject[] backgrounds;
        public GameObject log;
        public int currentIndex = 0;
        

        void Start()
        {
            UpdateBackground();
        }

        public void BackGroundPreference()
        {
            currentIndex = 0;
            UpdateBackground();
        }

        public void Apply()
        {
            if (currentIndex == 0)
            {
                log.SetActive(true);
                log.GetComponent<BackGroundLog>().index = 0;
                backgrounds[0].GetComponent<BackGroundPreference>().Apply();
            }
        }

        

        private void UpdateBackground()
        {
            for (int i = 0; i < backgrounds.Length; i++)
            {
                backgrounds[i].SetActive(i == currentIndex);
            }
        }
    }
}
