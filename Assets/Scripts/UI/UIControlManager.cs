using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class UIControlManager : MonoBehaviour
{
    [Header("UI Buttons")]
    public Button startButton;
    public Button resetButton;
    public Button stopButton;

    [Header("Physics Sliders")]
    public Slider gravitySlider;
    public Slider stretchSlider;
    public Slider volumeSlider;

    [Header("Slider Labels")]
    public TextMeshProUGUI gravityValueText;
    public TextMeshProUGUI stretchValueText;
    public TextMeshProUGUI volumeValueText;

    private PBDSimulator[] simulators;
    private bool isStopped = false;

    [Header("Velocity UI")]
    public TMP_InputField speedInput;
    public TMP_Dropdown directionDropdown;
    public Button applyVelocityButton;

    void Start()
    {
        simulators = FindObjectsOfType<PBDSimulator>();

        startButton.onClick.AddListener(OnStartClicked);
        resetButton.onClick.AddListener(OnResetClicked);
        stopButton.onClick.AddListener(OnStopClicked);
        applyVelocityButton.onClick.AddListener(OnApplyVelocityClicked);


        gravitySlider.onValueChanged.AddListener((v) => {
            ChangeGravity(v);
            UpdateGravityLabel(v);
        });

        stretchSlider.onValueChanged.AddListener((v) => {
            ChangeStretchStiffness(v);
            UpdateStretchLabel(v);
        });

        volumeSlider.onValueChanged.AddListener((v) => {
            ChangeVolumeStiffness(v);
            UpdateVolumeLabel(v);
        });

        // Set initial UI values
        gravitySlider.value = Mathf.Abs(Physics.gravity.y);

        if (simulators.Length > 0)
        {
            stretchSlider.value = simulators[0].stretchStiffness;
            volumeSlider.value = simulators[0].volumeStiffness;
        }

        UpdateGravityLabel(gravitySlider.value);
        UpdateStretchLabel(stretchSlider.value);
        UpdateVolumeLabel(volumeSlider.value);
    }
void OnApplyVelocityClicked()
{
    if (!float.TryParse(speedInput.text, out float speed))
    {
        Debug.LogWarning("[UI] Invalid speed input");
        return;
    }

    Vector3 dir = Vector3.zero;
    switch (directionDropdown.value)
    {
        case 0: dir = Vector3.up; break;      // Top
        case 1: dir = Vector3.down; break;    // Bottom
        case 2: dir = Vector3.left; break;    // Left
        case 3: dir = Vector3.right; break;   // Right
        case 4: dir = Vector3.forward; break; // Forward
        case 5: dir = Vector3.back; break;    // Backward
    }

    Vector3 velocity = dir.normalized * speed;

    foreach (var sim in simulators)
    {
        sim.SetInitialVelocity(velocity);
        sim.SimulatePBD(Time.fixedDeltaTime); 
    }

    Debug.Log($"[UI] Applied velocity: {velocity}");
}

    void OnStartClicked()
    {
        if (isStopped)
        {
            foreach (var sim in simulators)
                sim.RestartSimulation();

            isStopped = false;
        }

        Time.timeScale = 1;
    }

    void OnResetClicked()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
        Debug.Log("[UI] Reset");
    }

    void OnStopClicked()
    {
        isStopped = true;
        Time.timeScale = 0;
    }

    void ChangeGravity(float value)
    {
        Physics.gravity = new Vector3(0, -value, 0);
        Debug.Log($"[UI] Gravity set to: {-value}");
    }

    void ChangeStretchStiffness(float value)
    {
        foreach (var sim in simulators)
            sim.stretchStiffness = value;

        Debug.Log($"[UI] Stretch stiffness set to: {value}");
    }

    void ChangeVolumeStiffness(float value)
    {
        foreach (var sim in simulators)
            sim.volumeStiffness = value;

        Debug.Log($"[UI] Volume stiffness set to: {value}");
    }

    void UpdateGravityLabel(float value)
    {
        gravityValueText.text = $"Gravity: -{value:0.00}";
    }

    void UpdateStretchLabel(float value)
    {
        stretchValueText.text = $"Stretch: {value:0.00}";
    }

    void UpdateVolumeLabel(float value)
    {
        volumeValueText.text = $"Volume: {value:0.00}";
    }
}
