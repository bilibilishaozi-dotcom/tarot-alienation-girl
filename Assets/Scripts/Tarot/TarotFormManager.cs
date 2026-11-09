using UnityEngine;
using UnityEngine.InputSystem;

// 只负责「看起来变成哪一张牌」。不改位置、不改状态机、不改刚体和碰撞、不改朝向。
public class TarotFormManager : MonoBehaviour
{
    const string FormIdProperty = "_FormId";

    public TarotForm CurrentForm { get; private set; } = TarotForm.Original;
    public TarotFormData CurrentData { get; private set; }

    readonly System.Collections.Generic.Dictionary<TarotForm, TarotFormData> catalog =
        new System.Collections.Generic.Dictionary<TarotForm, TarotFormData>();

    SpriteRenderer body;
    Material runtimeMaterial;
    GameObject spawnedEffect;
    bool applied;
    GUIStyle labelStyle;

    public static TarotFormManager Ensure(Player player)
    {
        if (player == null)
            return null;

        TarotFormManager manager = player.GetComponent<TarotFormManager>();
        if (manager == null)
            manager = player.gameObject.AddComponent<TarotFormManager>();
        return manager;
    }

    void Awake()
    {
        LoadCatalog();
        TryBind();
    }

    void Start()
    {
        TryBind();
        if (!applied)
            ChangeForm(TarotForm.Original);
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame)
            return;

        TarotForm next = CurrentForm == TarotForm.Fool ? TarotForm.Original : TarotForm.Fool;
        ChangeForm(next);
    }

    public bool ChangeForm(TarotForm form)
    {
        TryBind();

        if (!catalog.TryGetValue(form, out TarotFormData data) || data == null)
        {
            Debug.LogWarning("塔罗形态还没有数据：" + form + "。死亡和月亮这版先空着。");
            return false;
        }

        CurrentForm = form;
        CurrentData = data;
        ApplyPalette(data.paletteId, data.paletteMaterial != null ? data.paletteMaterial : AnyPaletteMaterial());
        SwapEffect(data.effectPrefab);
        applied = true;
        return true;
    }

    void LoadCatalog()
    {
        catalog.Clear();
        TarotFormData[] all = Resources.LoadAll<TarotFormData>("Tarot");
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            catalog[all[i].form] = all[i];
        }
    }

    void TryBind()
    {
        if (body != null)
            return;

        WitchFrames frames = GetComponentInChildren<WitchFrames>(true);
        if (frames == null || frames.Body == null)
            return;

        body = frames.Body;
    }

    void ApplyPalette(int paletteId, Material source)
    {
        if (body == null || source == null)
            return;

        if (runtimeMaterial == null || runtimeMaterial.shader != source.shader)
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);

            runtimeMaterial = new Material(source);
            runtimeMaterial.name = "TarotPaletteRuntime";
        }

        runtimeMaterial.CopyPropertiesFromMaterial(source);
        if (runtimeMaterial.HasProperty(FormIdProperty))
            runtimeMaterial.SetFloat(FormIdProperty, paletteId);

        if (body.sharedMaterial != runtimeMaterial)
            body.sharedMaterial = runtimeMaterial;
    }

    Material AnyPaletteMaterial()
    {
        foreach (TarotFormData data in catalog.Values)
        {
            if (data != null && data.paletteMaterial != null)
                return data.paletteMaterial;
        }

        return null;
    }

    void SwapEffect(GameObject prefab)
    {
        if (spawnedEffect != null)
        {
            Destroy(spawnedEffect);
            spawnedEffect = null;
        }

        if (prefab == null)
            return;

        spawnedEffect = Instantiate(prefab, transform);
        spawnedEffect.transform.localPosition = Vector3.zero;
        spawnedEffect.transform.localRotation = Quaternion.identity;
    }

    void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.box);
            labelStyle.fontSize = 20;
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.normal.textColor = Color.white;
        }

        string name = CurrentData != null && !string.IsNullOrEmpty(CurrentData.displayName)
            ? CurrentData.displayName
            : CurrentForm.ToString();
        GUI.Box(new Rect(16f, Screen.height - 52f, 340f, 36f), "  F 切换形态：" + name, labelStyle);
    }
}
