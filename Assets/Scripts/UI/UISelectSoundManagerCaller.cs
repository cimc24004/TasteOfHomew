using UnityEngine;

public class UISelectSoundManagerCaller : MonoBehaviour
{
    private UISelectSoundManager uISelectSoundManager;
    public void UISelect()
    {
        UISelectSoundManager.instance.SelectPlaySound();
    }
    public void UISubmit()
    {
        UISelectSoundManager.instance.SubmitPlaySound();
    }
}
