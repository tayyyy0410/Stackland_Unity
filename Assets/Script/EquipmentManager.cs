using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }


    [System.Serializable]
    public class VillagerEquipState
    {
        public Card head;
        public Card hand;
        public Card body;
    }

    public Dictionary<Card,  VillagerEquipState> allEquipStates = new Dictionary<Card, VillagerEquipState> ();

    /// <summary>
    /// villager 有没有至少一件装备
    /// </summary>

    public bool HasAnyEquip(Card v)
    {
        if (v == null) return false;
        if (!allEquipStates.TryGetValue(v, out var state)) return false;
        bool hasEquip = state.head != null ||
                        state.hand != null ||
                        state.body != null;

        return state != null && hasEquip;
    }

    public VillagerEquipState GetEquipState(Card v)
    {
        if (v == null) return null;
        if (allEquipStates.TryGetValue(v, out var state))
        {
            return state;
        }
        return null;
    }





    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

    }
}
