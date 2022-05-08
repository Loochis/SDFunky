using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlipPane : MonoBehaviour
{
    public FlipPaneManager manager;

    public bool finishedFlip = false;
    public bool open = false;

    private Animator flipAnim;

    

    private void Start()
    {
        
        flipAnim = GetComponent<Animator>();
    }

    public void Flip()
    {
        flipAnim.SetTrigger("Flip");
        open = !open;
    }

    public void flipped()
    {
        finishedFlip = true;
        manager.PaneFlipped();
    }
}
