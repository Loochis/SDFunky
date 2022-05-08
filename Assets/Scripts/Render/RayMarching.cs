using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RayMarching : MonoBehaviour
{
    private Camera _camera;

    public ComputeShader rayMarchCShader;
    private RenderTexture _target;

    [Header("March Vars")]
    [Range(1, 1000)]
    public int marchDepth = 100;
    [Range(0.0001f, 0.01f)]
    public float epsilon = 0.01f;
    [Range(0, 1)]
    public float smoothAmount = 0;
    public float maxDepth = 20;

    [Header("Render Vars")]
    [Range(0,10)]
    public float ambientComp = 0.5f;
    [Range(0, 10)]
    public float diffuseComp = 0.5f;
    [Range(0, 10)]
    public float specularComp = 0.5f;
    [Range(2, 1000)]
    public float shininessConst = 1f;

    [Header("Mandelbulb Vars")]
    public int bulbIterations = 20;
    public float bulbBailout = 2;
    public float bulbPow = 1;

    private SDFEdit liveEdit;
    public Vector3 editPos = Vector3.zero;
    public Quaternion editRot = Quaternion.identity;
    public Vector3 editScale = Vector3.one;

    // Represents an SDF Edit
    public struct SDFEdit
    {
        public uint opType;         // 0: Union, 1: Intersect, 2: SubThis, 3: SubThat
        public float smoothing;     // Amount to smooth
        public uint shape;          // shape this edit represents
        public Matrix4x4 iMatrix;   // inverse TransformMatrix of the shape
        public Vector4 args1;       // shape args1 (radius, rounding, etc...)
        public Vector4 args2;       // shape args2 (radius, rounding, etc...)
    }

    // 4 bytes, 1 float, 1 uint, 2 Matrix4x4, 1 Vector4
    private const int sdfSize = sizeof(float) + sizeof(uint)*2 + sizeof(float) * 16 + sizeof(float) * 4 * 2;

    public SDFEdit[] sdfEditStack;
    private int stackLength = 0;

    public Transform sdfReaderParent;

    public int selected = -1;
    public int erroring = -1;

    Vector3 glowColour = Vector3.zero;

    public GameObject[] editorPrefabs;
    private FlipPaneManager[] editors;

    public GameObject firstPanel;
    public GameObject normalPanel;
    public GameObject delPanel;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        sdfEditStack = new SDFEdit[0];
        editors = new FlipPaneManager[0];

        /*
        RayMarching.SDFEdit edit = new RayMarching.SDFEdit();

        edit.shape = 0u;
        edit.smoothing = 0;
        edit.opType = 0u;
        edit.iMatrix = Matrix4x4.identity.inverse;
        edit.args1 = Vector4.one;
        edit.args2 = Vector4.one;

        sdfEditStack[0] = edit;
        */
    }

    public void RemoveStackIndex(FlipPaneManager oldManager)
    {
        int delIndex = oldManager.index;
        for (int i = 0; i < editors.Length; i++)
        {
            if (i > delIndex)
                editors[i].index--;
        }

        SDFEdit[] tempStack = new SDFEdit[sdfEditStack.Length - 1];
        int offset = 0;
        for (int i = 0; i < tempStack.Length; i++)
        {
            if (i == delIndex)
                offset = 1;
            tempStack[i] = sdfEditStack[i + offset];
        }
        sdfEditStack = tempStack;
        stackLength--;


        editors[delIndex] = null;
        FlipPaneManager[] newArr = new FlipPaneManager[editors.Length - 1];
        offset = 0;
        for (int i = 0; i < newArr.Length; i++)
        {
            if (i == delIndex)
                offset = 1;
            newArr[i] = editors[i + offset];
        }

        editors = newArr;

        Destroy(oldManager.gameObject);
    }

    public void ReplaceStackindex(FlipPaneManager oldManager, int newSDF)
    {
        GameObject createdEditor = Instantiate(editorPrefabs[newSDF], sdfReaderParent);
        FlipPaneManager cMan = createdEditor.GetComponent<FlipPaneManager>();
        cMan.rayMarchMaster = this;
        cMan.index = oldManager.index;
        createdEditor.transform.SetSiblingIndex(oldManager.index);
        for (int i = 0; i < 5; i++)
        {
            Debug.Log(i);
            string test = (oldManager.transformVars[i].ToString());
            cMan.transformInputs[i].SetTextWithoutNotify(test);
        }

        for (int i = 0; i < 8; i++)
        {
            if (cMan.argInputs[i] != null)
                cMan.argInputs[i].text = "1.0";
        }
        cMan.shapePicker.SetValueWithoutNotify(newSDF);
        cMan.smoothingSlider.SetValueWithoutNotify(oldManager.smoothingSlider.value);
        GameObject cBTN;
        if (oldManager.index == 0)
        {
            cBTN = Instantiate(firstPanel, cMan.transform);
            cBTN.transform.Find("ShapeVis").GetComponent<Image>().sprite = cMan.shapeSprites[newSDF];
        } else
        {
            cBTN = Instantiate(normalPanel, cMan.transform);
            cMan.smoothnessSpriteApp = cBTN.GetComponent<Image>();
            cBTN.transform.Find("ShapeVis").GetComponent<Image>().sprite = cMan.shapeSprites[newSDF];
            cMan.opSpritesApp = cBTN.transform.Find("IntersectVis").GetComponent<Image>();

            GameObject dBTN = Instantiate(delPanel, createdEditor.transform);
            dBTN.transform.SetAsLastSibling();
            dBTN.GetComponent<FlipPane>().manager = cMan;

            FlipPane[] tempArr = new FlipPane[cMan.panes.Length + 1];
            for (int i = 0; i < cMan.panes.Length; i++)
            {
                tempArr[i] = cMan.panes[i];
            }
            tempArr[cMan.panes.Length] = dBTN.GetComponent<FlipPane>();
            cMan.panes = tempArr;

            dBTN.GetComponent<Button>().onClick.AddListener(delegate () { RemoveStackIndex(cMan); });
        }

        cBTN.transform.SetAsFirstSibling();
        cBTN.GetComponent<Button>().onClick.AddListener(delegate () { cMan.ActivateFlip(); });
        cMan.activatingButton = cBTN.GetComponent<Button>();

        editors[oldManager.index] = cMan;

        Destroy(oldManager.gameObject);
    }

    public void AddEditToStack()
    {
        SDFEdit edit = new SDFEdit();

        edit.opType = 0u;
        edit.smoothing = 0f;
        edit.shape = 0u;
        edit.iMatrix = Matrix4x4.identity.inverse;
        edit.args1 = Vector4.one;
        edit.args2 = Vector4.one;

        GameObject createdEditor = Instantiate(editorPrefabs[0], sdfReaderParent);
        FlipPaneManager cMan = createdEditor.GetComponent<FlipPaneManager>();
        cMan.rayMarchMaster = this;
        cMan.index = stackLength;
        createdEditor.transform.SetSiblingIndex(stackLength);

        GameObject cBTN;
        if (stackLength == 0)
        {
            cBTN = Instantiate(firstPanel, createdEditor.transform);
        }
        else
        {
            cBTN = Instantiate(normalPanel, createdEditor.transform);
            cMan.smoothnessSpriteApp = cBTN.GetComponent<Image>();
            GameObject dBTN = Instantiate(delPanel, createdEditor.transform);
            dBTN.transform.SetAsLastSibling();
            dBTN.GetComponent<FlipPane>().manager = cMan;

            FlipPane[] tempArr = new FlipPane[cMan.panes.Length + 1];
            for (int i = 0; i < cMan.panes.Length; i++)
            {
                tempArr[i] = cMan.panes[i];
            }
            tempArr[cMan.panes.Length] = dBTN.GetComponent<FlipPane>();
            cMan.panes = tempArr;

            dBTN.GetComponent<Button>().onClick.AddListener(delegate () { RemoveStackIndex(cMan); });
        }
        cBTN.transform.SetAsFirstSibling();
        cBTN.GetComponent<Button>().onClick.AddListener(delegate () { cMan.ActivateFlip(); });
        cMan.activatingButton = cBTN.GetComponent<Button>();

        stackLength++;

        // Copy array into new, append edit to end
        SDFEdit[] newStack = new SDFEdit[sdfEditStack.Length + 1];
        for (int i = 0; i < sdfEditStack.Length; i++)
        {
            newStack[i] = sdfEditStack[i];
        }
        newStack[newStack.Length - 1] = edit;

        // Set sdfEditStack to the new stack
        sdfEditStack = newStack;


        FlipPaneManager[] newArr = new FlipPaneManager[editors.Length + 1];
        for (int i = 0; i < editors.Length; i++)
        {
            newArr[i] = editors[i];
        }
        newArr[newArr.Length - 1] = cMan;
        editors = newArr;
    }

    private void Start()
    {
        AddEditToStack();
    }

    private void Update()
    {
        Matrix4x4 editMatrix = Matrix4x4.TRS(editPos, editRot, editScale);
        liveEdit.iMatrix = editMatrix.inverse;
        //sdfEditStack[1] = liveEdit;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();
        // Set the target and dispatch the compute shader
        rayMarchCShader.SetTexture(0, "Result", _target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 32.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 16.0f);

        SetShaderParameters();

        // Set up compute buffer for edit stack

        ComputeBuffer editBuffer = new ComputeBuffer(sdfEditStack.Length, sdfSize);
        editBuffer.SetData(sdfEditStack);

        rayMarchCShader.SetInt("numEdits", sdfEditStack.Length);
        rayMarchCShader.SetBuffer(0, "SDFEditStack", editBuffer);

        rayMarchCShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        editBuffer.Dispose();

        // Blit the result texture to the screen
        Graphics.Blit(_target, destination);
    }

    private void SetShaderParameters()
    {
        // Sets the variables of the shader
        rayMarchCShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        rayMarchCShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        rayMarchCShader.SetInt("marchDepth", marchDepth);
        rayMarchCShader.SetFloat("epsilon", epsilon);
        rayMarchCShader.SetFloat("smoothAmount", smoothAmount);
        rayMarchCShader.SetFloat("maxDepth", maxDepth);

        rayMarchCShader.SetFloat("ka", ambientComp);
        rayMarchCShader.SetFloat("kd", diffuseComp);
        rayMarchCShader.SetFloat("ks", specularComp);
        rayMarchCShader.SetFloat("kn", shininessConst);

        rayMarchCShader.SetInt("bulbIters", bulbIterations);
        rayMarchCShader.SetFloat("bailout", bulbBailout);
        rayMarchCShader.SetFloat("power", bulbPow);

        rayMarchCShader.SetInt("selection", selected);
        rayMarchCShader.SetInt("erroring", erroring);

        Vector3 desiredGlow = Vector3.zero;
        if (selected > 0)
            desiredGlow = new Vector3(1f, 1f, 0.5f);
        if (selected == 0)
            desiredGlow = new Vector3(0.5f, 1f, 1f);
        if (erroring >= 0)
            desiredGlow = new Vector3(1f, 0.2f, 0.2f);
        glowColour = Vector3.Lerp(glowColour, desiredGlow, Time.deltaTime*5);

        rayMarchCShader.SetVector("glowCol", glowColour);

    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();
            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }
}
