namespace Quantum.Asteroids {
  using Photon.Deterministic;
  using UnityEngine;
#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM
  using UnityEngine.InputSystem;
#endif

  /// <summary>
  /// The <c>AsteroidsQuantumInput</c> class handles the input for the Asteroids game by subscribing to Quantum's input callback system.
  /// It's more complicated that needed because it supports both the new Input System and the legacy Input Manager.
  /// The essence of this class is the <c>OnPollInput</c> method that reads the current input state and sets it in the Quantum input callback.
  /// </summary>
  public class AsteroidsQuantumInput : QuantumMonoBehaviour {
    public GameObject MobileControlsPrefab;
    public Canvas MobileControlsCanvas;

#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM
    private GameObject _mobileControls;
    private PlayerInput _playerInput;
    private InputAction _move;
    private InputAction _fire;
#endif

    /// <summary>
    /// Get or create the PlayerInput component. Uses the Quantum default input action asset.
    /// Caches the individual input actions to query inside the Quantum input polling callback.
    /// </summary>
    private void Awake() {
#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM
      if (TryGetComponent<PlayerInput>(out _playerInput) == false) {
        _playerInput = gameObject.AddComponent<PlayerInput>();
        _playerInput.actions = QuantumDefaultConfigs.Global.InputActionAsset;
        _playerInput.actions.Enable();
      }

      _move = _playerInput.actions["Move"];
      _fire = _playerInput.actions["Jump"];
#endif
    }

    /// <summary>
    /// Subscribes to the Quantum input callback when the script is enabled.
    /// </summary>
    private void OnEnable() {
      QuantumCallback.Subscribe(this, (CallbackGameStarted callback) => OnGameStarted(callback));
      QuantumCallback.Subscribe(this, (CallbackGameDestroyed callback) => OnGameDestroyed(callback));
      QuantumCallback.Subscribe(this, (CallbackPollInput callback) => OnPollInput(callback));
    }

    /// <summary>
    /// Polls the current input state and sets it in the Quantum input callback.
    /// </summary>
    /// <param name="callback">The input callback provided by Quantum.</param>
    private void OnPollInput(CallbackPollInput callback) {
      Quantum.Input input = new Quantum.Input();

#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM
      var move = _move.ReadValue<Vector2>();
      input.Fire = _fire.IsPressed();
      input.Left = move.x < -0.7f;
      input.Right = move.x > 0.7f;
      input.Up = move.y > 0.7f;
#elif ENABLE_LEGACY_INPUT_MANAGER
      input.Left = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
      input.Right = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);
      input.Up = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
      input.Fire = UnityEngine.Input.GetKey(KeyCode.Space);
#endif

      callback.SetInput(input, DeterministicInputFlags.Repeatable);
    }

    /// <summary>
    /// Create and toggle on mobile controls when the game starts.
    /// </summary>
    private void OnGameStarted(CallbackGameStarted callback) {
#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM && (UNITY_IOS || UNITY_ANDROID)
      if (_mobileControls == null) {
        _mobileControls = Instantiate(MobileControlsPrefab, MobileControlsCanvas.transform);
      }

      _mobileControls?.SetActive(true);
      // Mobile controls are disabled when starting the scene.
      // Toggle the player input to make stick controls to be mapped to actions.
      _playerInput.enabled = false;
      _playerInput.enabled = true;
#endif
    }

    /// <summary>
    /// Toggle mobile controls off.
    /// </summary>
    /// <param name="callback"></param>
    private void OnGameDestroyed(CallbackGameDestroyed callback) {
#if ENABLE_INPUT_SYSTEM && QUANTUM_ENABLE_INPUTSYSTEM && (UNITY_IOS || UNITY_ANDROID)
      _mobileControls?.SetActive(false);
#endif
    }
  }
}