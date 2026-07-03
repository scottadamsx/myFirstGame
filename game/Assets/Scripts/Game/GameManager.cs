using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// Central runtime brain. Spawned automatically by GameBootstrap when a city
/// scene starts playing — owns city data, coordinate mapping, subsystems,
/// and save/load (F5 / F9).
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public CityData City { get; private set; }
    public CoordinateMapper Mapper { get; private set; }
    public SimpleWalker Player { get; private set; }
    public bool Ready { get; private set; }
    public int Loonies = 25;   // starting pocket money

    [Serializable]
    class SaveData
    {
        public float px, py, pz;
        public float hour;
        public int questStep;
        public int loonies;
        public int[] puffins;
        public int[] itemCounts;
        public int missionsMask;
    }

    string SavePath => Path.Combine(Application.persistentDataPath, "stjohns_save.json");

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Player = FindFirstObjectByType<SimpleWalker>();
        City = CityData.Load();
        Mapper = new CoordinateMapper();
        Physics.SyncTransforms();
        Mapper.Calibrate(City);

        gameObject.AddComponent<VisualUpgrade>();
        gameObject.AddComponent<DayNightCycle>();
        gameObject.AddComponent<VehicleManager>();
        gameObject.AddComponent<TrafficSystem>();
        gameObject.AddComponent<PedestrianSystem>();
        gameObject.AddComponent<QuestSystem>();
        gameObject.AddComponent<TaxiSystem>();
        gameObject.AddComponent<Collectibles>();
        gameObject.AddComponent<Inventory>();
        gameObject.AddComponent<MissionManager>();
        gameObject.AddComponent<GameHUD>();
        Ready = true;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.f5Key.wasPressedThisFrame) Save();
        if (kb.f9Key.wasPressedThisFrame) Load();
    }

    public Vector3 PlayerPosition()
    {
        var vm = GetComponent<VehicleManager>();
        if (vm != null && vm.DrivenCar != null) return vm.DrivenCar.transform.position;
        return Player != null ? Player.transform.position : Vector3.zero;
    }

    void Save()
    {
        var data = new SaveData
        {
            px = PlayerPosition().x,
            py = PlayerPosition().y,
            pz = PlayerPosition().z,
            hour = GetComponent<DayNightCycle>().hour,
            questStep = GetComponent<QuestSystem>().CurrentStep,
            loonies = Loonies,
            puffins = GetComponent<Collectibles>().Export(),
            itemCounts = GetComponent<Inventory>().ExportCounts(),
            missionsMask = GetComponent<MissionManager>().ExportMask(),
        };
        File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        GameHUD.Toast("Saved, b'y.");
    }

    void Load()
    {
        if (!File.Exists(SavePath)) { GameHUD.Toast("No save yet (F5 saves)."); return; }
        var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
        var vm = GetComponent<VehicleManager>();
        if (vm != null && vm.DrivenCar != null) vm.ExitCar();
        if (Player != null)
        {
            var cc = Player.GetComponent<CharacterController>();
            cc.enabled = false;
            Player.transform.position = new Vector3(data.px, data.py + 0.5f, data.pz);
            cc.enabled = true;
        }
        GetComponent<DayNightCycle>().hour = data.hour;
        GetComponent<QuestSystem>().SetStep(data.questStep);
        Loonies = data.loonies;
        GetComponent<Collectibles>().Apply(data.puffins);
        GetComponent<Inventory>().ApplyCounts(data.itemCounts);
        GetComponent<MissionManager>().ApplyMask(data.missionsMask);
        GameHUD.Toast("Loaded.");
    }
}
