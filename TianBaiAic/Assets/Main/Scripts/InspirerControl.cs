using UnityEngine;

public class InspirerControl : MonoBehaviour
{
    public GameObject inspirer;
    public GameObject debug_inspirer;
    public GameObject exp_set_inspirer;
    public GameObject SetGameObject;

    public Camera main_camera;

    bool mouseDown = false;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.F5))
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                //��ʾ��������Inspirer
                if (inspirer.activeSelf)
                {
                    inspirer.SetActive(false);
                }
                else
                {
                    inspirer.SetActive(true);
                }
            }
            if (Input.GetKeyDown(KeyCode.F))
            {
                //��ʾ��������Debug Inspirer
                if (debug_inspirer.activeSelf)
                {
                    debug_inspirer.SetActive(false);
                }
                else
                {
                    debug_inspirer.SetActive(true);
                }
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                //��ʾ��������Exp Set Inspirer
                if (exp_set_inspirer.activeSelf)
                {
                    exp_set_inspirer.SetActive(false);
                }
                else
                {
                    exp_set_inspirer.SetActive(true);
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.T))
        {
            //��ʾ��������Set GameObject
            if (SetGameObject.activeSelf)
            {
                SetGameObject.SetActive(false);
            }
            else
            {
                SetGameObject.SetActive(true);
            }
        }
        
        //�û��϶�Inspirerλ��
            if (Input.GetMouseButton(0))
            {
                //�������Ƿ���Inspirer(Ui)��
                if (Input.mousePosition.x > inspirer.GetComponent<RectTransform>().position.x - 425 && Input.mousePosition.x < inspirer.GetComponent<RectTransform>().position.x + 425 &&
                    Input.mousePosition.y > inspirer.GetComponent<RectTransform>().position.y - 425 && Input.mousePosition.y < inspirer.GetComponent<RectTransform>().position.y + 425)
                {
                    mouseDown = true;
                }
            }
        if (Input.GetMouseButtonUp(0) && mouseDown)
        {
            mouseDown = false;
        }
        if (mouseDown)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = 10f; // Set a distance from the camera
            Vector3 worldPos = main_camera.ScreenToWorldPoint(mousePos);
            inspirer.GetComponent<RectTransform>().position = new Vector3(worldPos.x, worldPos.y, inspirer.GetComponent<RectTransform>().position.z);
        }
    }
}
