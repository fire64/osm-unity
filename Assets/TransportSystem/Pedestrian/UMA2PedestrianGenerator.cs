using System.Collections.Generic;
using UMA;
using UMA.CharacterSystem;
using UnityEngine;

public class UMA2PedestrianGenerator : MonoBehaviour
{
    [Header("UMA 2 Settings")]
    public DynamicCharacterAvatar umaAvatar;
    public float generationDelay = 0.5f;

    [Header("Appearance Settings")]
    public bool randomizeAppearance = true;

    private bool isUMAGenerated = false;

    public List<UMA.UMARandomizer> Randomizers;

    void Start()
    {
        if (umaAvatar == null)
        {
            umaAvatar = GetComponent<DynamicCharacterAvatar>();
        }

        if (umaAvatar != null && randomizeAppearance)
        {
            // Подписываемся на события UMA
            umaAvatar.CharacterCreated.AddListener(OnCharacterCreated);
            umaAvatar.CharacterUpdated.AddListener(OnCharacterUpdated);

            // Ждем немного перед генерацией, чтобы UMA успел инициализироваться
            Invoke("GenerateRandomUMA", generationDelay);
        }
    }

    public RandomWardrobeSlot GetRandomWardrobe(List<RandomWardrobeSlot> wardrobeSlots)
    {
        int total = 0;

        for (int i = 0; i < wardrobeSlots.Count; i++)
        {
            RandomWardrobeSlot rws = wardrobeSlots[i];
            total += rws.Chance;
        }

        for (int i = 0; i < wardrobeSlots.Count; i++)
        {
            RandomWardrobeSlot rws = wardrobeSlots[i];
            if (UnityEngine.Random.Range(0, total) < rws.Chance)
            {
                return rws;
            }
        }
        return wardrobeSlots[wardrobeSlots.Count - 1];
    }

    private OverlayColorData GetRandomColor(RandomColors rc)
    {
        int inx = UnityEngine.Random.Range(0, rc.ColorTable.colors.Length);
        return rc.ColorTable.colors[inx];
    }

    private void AddRandomSlot(DynamicCharacterAvatar Avatar, RandomWardrobeSlot uwr)
    {
        Avatar.SetSlot(uwr.WardrobeSlot);
        if (uwr.Colors != null)
        {
            for (int i = 0; i < uwr.Colors.Count; i++)
            {
                RandomColors rc = uwr.Colors[i];
                if (rc.ColorTable != null)
                {
                    OverlayColorData ocd = GetRandomColor(rc);
                    Avatar.SetColor(rc.ColorName, ocd, false);
                }
            }
        }
    }

    public void Randomize(DynamicCharacterAvatar Avatar)
    {
        // Must clear that out!
        Avatar.WardrobeRecipes.Clear();

        UMARandomizer Randomizer = null;
        if (Randomizers != null)
        {
            if (Randomizers.Count == 0)
            {
                return;
            }

            if (Randomizers.Count == 1)
            {
                Randomizer = Randomizers[0];
            }
            else
            {
                Randomizer = Randomizers[UnityEngine.Random.Range(0, Randomizers.Count)];
            }
        }
        if (Avatar != null && Randomizer != null)
        {
            RandomAvatar ra = Randomizer.GetRandomAvatar();
            Avatar.ChangeRaceData(ra.RaceName);
            //Avatar.BuildCharacterEnabled = true;
            var RandomDNA = ra.GetRandomDNA();
            Avatar.predefinedDNA = RandomDNA;
            var RandomSlots = ra.GetRandomSlots();

            if (ra.SharedColors != null && ra.SharedColors.Count > 0)
            {
                for (int i = 0; i < ra.SharedColors.Count; i++)
                {
                    RandomColors rc = ra.SharedColors[i];
                    if (rc.ColorTable != null)
                    {
                        Avatar.SetColor(rc.ColorName, GetRandomColor(rc), false);
                    }
                }
            }
            foreach (string s in RandomSlots.Keys)
            {
                List<RandomWardrobeSlot> RandomWardrobe = RandomSlots[s];
                RandomWardrobeSlot uwr = GetRandomWardrobe(RandomWardrobe);
                if (uwr.WardrobeSlot != null)
                {
                    AddRandomSlot(Avatar, uwr);
                }
            }
        }
    }

    public void GenerateRandomUMA()
    {
        if (umaAvatar == null)
            return;

        Randomize(umaAvatar);
        umaAvatar.BuildCharacter(!umaAvatar.BundleCheck);
    }

    void OnCharacterCreated(UMAData data)
    {
        RandomizeUMAAppearance(data);
    }

    void OnCharacterUpdated(UMAData data)
    {
        if (!isUMAGenerated)
        {
            isUMAGenerated = true;
            // Дополнительные настройки после обновления
        }
    }

    void RandomizeUMAAppearance(UMAData data)
    {
        if (umaAvatar == null)
            return;

    }

    // Метод для принудительной повторной генерации
    public void Regenerate()
    {
        isUMAGenerated = false;
        GenerateRandomUMA();
    }
}