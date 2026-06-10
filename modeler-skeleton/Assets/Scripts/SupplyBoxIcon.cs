using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class SupplyBoxIcon : MonoBehaviour
{
    [Tooltip("Station this icon belongs to. If null, looks on the parent.")]
    public Station station;

    public Texture2D carrotIcon;
    public Texture2D onionIcon;
    public Texture2D tomatoIcon;

    [Tooltip("Hide the quad when the SupplyBox has no supplyPrefab assigned.")]
    public bool hideWhenEmpty = true;

    private MeshRenderer mr;
    private MaterialPropertyBlock mpb;
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
        mpb = new MaterialPropertyBlock();
        if (station == null) station = GetComponentInParent<Station>();
    }

    void OnEnable() { Refresh(); }

    public void Refresh()
    {
        if (mr == null) mr = GetComponent<MeshRenderer>();
        if (station == null || station.supplyPrefab == null || station.supplyPrefab.kind != PickupableKind.Food)
        {
            if (hideWhenEmpty) mr.enabled = false;
            return;
        }

        Texture2D tex = PickIcon(station.supplyPrefab.vegetableType);
        if (tex == null)
        {
            if (hideWhenEmpty) mr.enabled = false;
            return;
        }

        mr.enabled = true;
        mr.GetPropertyBlock(mpb);
        mpb.SetTexture(BaseMapId, tex);
        mpb.SetTexture(MainTexId, tex);
        mr.SetPropertyBlock(mpb);
    }

    private Texture2D PickIcon(VegetableType v)
    {
        switch (v)
        {
            case VegetableType.Carrot: return carrotIcon;
            case VegetableType.Onion:  return onionIcon;
            case VegetableType.Tomato: return tomatoIcon;
            default: return null;
        }
    }

#if UNITY_EDITOR
    void OnValidate() { if (Application.isPlaying) Refresh(); }
#endif
}
