using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class EditorCamMove : MonoBehaviour
{

    public float rotSensitivity = 1000f;
    public float transSensitivity = 20f;
    public float scrollSensitivity = 1f;
    [Range(1, 100)]
    public int smoothing = 20;

    float xRotation = 0f;
    float yRotation = 0f;

    private int overwriteInput = 0;
    private Vector2[] prevInputs;

    public Vector3 orbitPoint = Vector3.zero;

    private float desiredScale = 0f;

    void Start()
    {
        prevInputs = new Vector2[smoothing];
        desiredScale = transform.localScale.x;
    }

    void CalcRotation()
    {
        Vector2 newInput;
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * rotSensitivity;
            newInput = new Vector2(mouseX, mouseY);
            prevInputs[overwriteInput] = newInput;
        }
        else
        {
            prevInputs[overwriteInput] = Vector2.zero;
        }

        overwriteInput++;
        if (overwriteInput >= smoothing)
            overwriteInput = 0;

        Vector2 totalInput = Vector2.zero;
        int inputsParsed = 0;
        for (int i = 0; i < smoothing; i++)
        {
            inputsParsed++;
            if (prevInputs[i] != null)
                totalInput += prevInputs[i];

        }
        totalInput /= inputsParsed;

        xRotation -= totalInput.y;
        yRotation += totalInput.x;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0);
    }

    void CalcTranslation()
    {
        Vector2 newInput = Vector2.zero;
        if (Input.GetMouseButton(2))
        {
            float mouseX = Input.GetAxis("Mouse X") * transSensitivity * desiredScale;
            float mouseY = Input.GetAxis("Mouse Y") * transSensitivity * desiredScale;
            newInput = new Vector2(mouseX, mouseY);
        }

        transform.position -= newInput.x * transform.right + newInput.y * transform.up;
    }

    void CalcZoom()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }
        float scrollAmount = Input.GetAxis("Mouse ScrollWheel") * scrollSensitivity;// * Time.deltaTime;

        desiredScale -= scrollAmount * desiredScale;
        desiredScale = Mathf.Clamp(desiredScale, 0.001f, 20f);
        transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(desiredScale, desiredScale, desiredScale), Time.deltaTime * 10);
    }

    void Update()
    {
        CalcRotation();
        CalcTranslation();
        CalcZoom();
    }
}
