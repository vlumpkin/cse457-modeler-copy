using UnityEngine;

// Lives on Character.prefab — the gameplay character. Holds the 4 animal child
// roots and a reference to the single shared hands renderer. GameSceneSpawner
// calls Apply(animalIndex, colorIndex) after PlayerInput.Instantiate so the
// correct animal is shown with the right hand material.
public class PlayerCharacterVisual : MonoBehaviour
{
    [Header("Animal roots (index matches PlayerSelections.animalIndex)")]
    [Tooltip("0=Cat, 1=Chicken, 2=Dog, 3=Frog. Apply() SetActives the chosen one and deactivates the rest.")]
    public GameObject[] animals = new GameObject[4];

    [Header("Hands (shared across all animals)")]
    [Tooltip("All hand renderers — typically left, right, and holding. All get the same color material.")]
    public Renderer[] handsRenderers = new Renderer[3];

    [Tooltip("Material slot on each hand renderer to override. Usually 0.")]
    public int handMaterialSlot = 0;

    [Header("Hand color materials (index matches PlayerSelections.colorIndex)")]
    [Tooltip("0=Blue, 1=Red, 2=Green, 3=Yellow.")]
    public Material[] handColorMaterials = new Material[4];

    public void Apply(int animalIndex, int colorIndex)
    {
        if (animals != null)
        {
            for (int i = 0; i < animals.Length; i++)
                if (animals[i] != null) animals[i].SetActive(i == animalIndex);
        }

        if (handsRenderers == null) return;
        if (handColorMaterials == null || colorIndex < 0 || colorIndex >= handColorMaterials.Length) return;
        Material mat = handColorMaterials[colorIndex];
        if (mat == null) return;

        for (int i = 0; i < handsRenderers.Length; i++)
        {
            var r = handsRenderers[i];
            if (r == null) continue;
            var mats = r.sharedMaterials;
            if (handMaterialSlot < 0 || handMaterialSlot >= mats.Length) continue;
            mats[handMaterialSlot] = mat;
            r.sharedMaterials = mats;
        }
    }
}
