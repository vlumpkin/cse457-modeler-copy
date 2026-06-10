using UnityEngine;

/// <summary>
/// Companion component for a Station whose kind == StationKind.DeliveryCounter.
/// Holds the pointer to the DirtyPlateReclaim station so OvercookedCharacter
/// can notify it when a soup is delivered.
///
/// Wiring: put on the same GameObject as the Station, drag the reclaim
/// station's DirtyPlateReclaim component into the field.
/// </summary>
[RequireComponent(typeof(Station))]
public class DeliveryCounter : MonoBehaviour
{
    [Tooltip("Where dirty plates come back after being processed. PlateProcessed() is called on this " +
             "every time a soup is delivered here. Leave null to skip the customer-return flow.")]
    public DirtyPlateReclaim reclaim;
}
