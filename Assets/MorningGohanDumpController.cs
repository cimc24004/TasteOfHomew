using UnityEngine;

public class MorningGohanDumpController : MonoBehaviour
{
    [SerializeField] private ItemEquipController itemEquipController;
    [SerializeField] private string ItemEquipMeatKey = "Meat";
    [SerializeField] private InteractEventRelay meatforHaveInteractEventRelay;
    private PhaseStepController phaseStepController;
    [SerializeField]
    private GameObject Meat;
    private AudioSource MeatGetAudioSource;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        phaseStepController = PhaseStepController.instance;
        itemEquipController = ItemEquipController.instance;
        MeatGetAudioSource = this.gameObject.GetComponent<AudioSource>();
        meatforHaveInteractEventRelay.onInteract.AddListener(MeatGohanGet);
    }


    public void MeatDumpEventStart()
    {
        meatforHaveInteractEventRelay.gameObject.SetActive(true);
    }
    public void MeatGohanGet()
    {
        //ご飯をゲットする。 interact EventRelayから呼ばれる。
        MeatGetAudioSource.Play();
        itemEquipController.SetHasItem(ItemEquipMeatKey, true);
        Meat.SetActive(false);
    }
    public void MeatGohanDump()
    {
        //ご飯をゴミ箱に捨てる。 PlayableCallから呼ばれます。 
        itemEquipController.ClearAll();
    }


}
