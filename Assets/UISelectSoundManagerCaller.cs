using UnityEngine;

public class UISelectSoundManagerCaller : MonoBehaviour
{
    private UISelectSoundManager uISelectSoundManager;
    public void UISelect()
    {
        UISelectSoundManager.Instance.SelectPlaySound();
    }
    public void UISubmit()
    {
        UISelectSoundManager.Instance.SubmitPlaySound();
    }
}
