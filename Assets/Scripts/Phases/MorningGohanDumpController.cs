using UnityEngine;

public class MorningGohanDumpController : MonoBehaviour
{
    [SerializeField] private ItemEquipController itemEquipController;
    [SerializeField] private string ItemEquipMeatKey = "Meat";
    [SerializeField] private InteractEventRelay meatforHaveInteractEventRelay;
    [SerializeField] private PlayerableCall DumpPlayableCall;
    private PhaseStepController phaseStepController;
    [SerializeField]
    private GameObject Meat;
    private AudioSource MeatGetAudioSource;
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
        DumpPlayableCall.gameObject.SetActive(false);
    }
    public void MeatGohanGet()
    {
        //ご飯をゲットする。 interact EventRelayから呼ばれる。
        MeatGetAudioSource.Play();
        itemEquipController.SetHasItem(ItemEquipMeatKey, true);
        Meat.SetActive(false);
        DumpPlayableCall.gameObject.SetActive(true);
    }
    public void MeatGohanDump()
    {
        //ご飯をゴミ箱に捨てる。 PlayableCallから呼ばれます。
        itemEquipController.ClearAll();
    }
}
