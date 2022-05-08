using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class FlipPaneManager : MonoBehaviour
{
    public FlipPane[] panes;
    public Button activatingButton;

    private bool open = false;

    private int lastPaneFlipped = -1;

    private RectTransform layoutToRebuild;

    public Dropdown shapePicker;
    public Dropdown opPicker;
    public Slider smoothingSlider;
    public InputField[] transformInputs;
    public InputField[] argInputs;

    public RayMarching rayMarchMaster;

    public GameObject firstPanel;
    public GameObject normalPanel;

    public int index = 0;

    public float[] transformVars;

    public Sprite[] smoothnessSprites;
    public Image smoothnessSpriteApp;

    public Sprite[] shapeSprites;
    public Image shapeSpriteApp;

    public Sprite[] opSprites;
    public Image opSpritesApp;

    private void Start()
    {
        layoutToRebuild = GetComponent<RectTransform>();
        transformVars = new float[6];
        for (int i = 0; i < 6; i++)
            transformVars[i] = 0f;

        for (int i = 0; i < panes.Length; i++)
        {
            panes[i].manager = this;
        }
    }

    public void ActivateFlip()
    {
        foreach(Transform child in transform.parent)
        {
            FlipPaneManager paneMan = child.GetComponent<FlipPaneManager>();
            if (paneMan != null && paneMan.open)
                paneMan.ClosePane();
        }

        if (open)
        {
            rayMarchMaster.selected = -1;
            lastPaneFlipped = panes.Length-1;
        }
        else
        {
            rayMarchMaster.selected = index;
            lastPaneFlipped = 0;
        }
        panes[lastPaneFlipped].Flip();
    }

    public void ClosePane()
    {
        rayMarchMaster.selected = -1;
        lastPaneFlipped = panes.Length - 1;
        panes[lastPaneFlipped].Flip();
        LayoutRebuilder.MarkLayoutForRebuild(layoutToRebuild);
    }

    public void PaneFlipped()
    {
        if (open)
        {
            lastPaneFlipped--;
            if (lastPaneFlipped == 1 && index == 0)
                lastPaneFlipped--;
        }
        else
        {
            lastPaneFlipped++;
            if (lastPaneFlipped == 1 && index == 0)
                lastPaneFlipped++;
        }

        if (lastPaneFlipped < 0 || lastPaneFlipped >= panes.Length)
        {
            open = !open;
            activatingButton.interactable = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutToRebuild);
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(layoutToRebuild);
        panes[lastPaneFlipped].Flip();
    }

    public void unflipDependentPanes()
    {
        for (int i = 2; i < panes.Length; i++)
        {
            if (panes[i].open)
                panes[i].Flip();
        }
    }

    private RayMarching.SDFEdit defaultSDF(uint shape)
    {
        RayMarching.SDFEdit edit = new RayMarching.SDFEdit();

        edit.shape = shape;
        edit.smoothing = 0;
        edit.opType = 0u;
        edit.iMatrix = Matrix4x4.identity.inverse;
        edit.args1 = Vector4.one;
        edit.args2 = Vector4.one;

        return edit;
    }

    public void TransformedInputsChanged()
    {
        try
        {
            for (int i = 0; i < 6; i++)
                transformVars[i] = float.Parse(transformInputs[i].text);

            Vector3 pos = new Vector3(transformVars[0], transformVars[1], transformVars[2]);
            Quaternion rot = Quaternion.Euler(transformVars[3], transformVars[4], transformVars[5]);
            rayMarchMaster.sdfEditStack[index].iMatrix = Matrix4x4.TRS(pos, rot, Vector3.one).inverse;
        } catch (FormatException fExc)
        {
            errorFound();
            return;
        }

        if (index == rayMarchMaster.erroring && CheckInputs())
            rayMarchMaster.erroring = -1;
    }

    public void NewShapePicked()
    {
        uint newSDF = (uint)shapePicker.value;
        rayMarchMaster.sdfEditStack[index].shape = newSDF;
        rayMarchMaster.ReplaceStackindex(this, (int)newSDF);
    }

    public void NewOpTypePicked()
    {
        rayMarchMaster.sdfEditStack[index].opType = (uint)opPicker.value;
        if (opSpritesApp != null)
            opSpritesApp.sprite = opSprites[opPicker.value];
    }

    public void NewSmoothValue()
    {
        rayMarchMaster.sdfEditStack[index].smoothing = smoothingSlider.value;
        if (smoothnessSpriteApp != null)
        {
            int spriteIndex = (int)Math.Round(smoothingSlider.value*(smoothnessSprites.Length-1));
            smoothnessSpriteApp.sprite = smoothnessSprites[spriteIndex];
        }
    }

    public void ArgsChanged()
    {
        try
        {
            float[] args = new float[8];
            for (int i = 0; i < 8; i++)
                args[i] = 1f;

            for (int arg = 0; arg < 8; arg++)
            {
                if (argInputs[arg] != null)
                    args[arg] = float.Parse(argInputs[arg].text);
            }

            rayMarchMaster.sdfEditStack[index].args1 = new Vector4(args[0], args[1], args[2], args[3]);
            rayMarchMaster.sdfEditStack[index].args2 = new Vector4(args[4], args[5], args[6], args[7]);
            }
        catch (FormatException fExc)
        {
            errorFound();
            return;
        }

        if (index == rayMarchMaster.erroring && CheckInputs())
            rayMarchMaster.erroring = -1;
    }

    private void errorFound()
    {
        if (rayMarchMaster.erroring == -1 || rayMarchMaster.erroring > index)
        {
            rayMarchMaster.erroring = index;
        }
    }

    public bool CheckInputs()
    {
        try
        {
            for (int i = 0; i < 6; i++)
                float.Parse(transformInputs[i].text);
            for (int i = 0; i < 8; i++)
            {
                if (argInputs[i] != null)
                    float.Parse(argInputs[i].text);
            }
        } catch (FormatException fExc)
        {
            return false;
        }
        return true;
    }
}
