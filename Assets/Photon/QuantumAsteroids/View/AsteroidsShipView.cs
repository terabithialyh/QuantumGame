namespace Quantum.Asteroids
{
  using UnityEngine;
  using Quantum;
#if QUANTUM_ENABLE_TEXTMESHPRO
  using Text = TMPro.TextMeshProUGUI;
#else 
  using Text = UnityEngine.UI.Text;
#endif

  /// <summary>
  /// The <c>AsteroidsShipView</c> class manages the visual representation of a ship in the Asteroids game.
  /// It updates the player name, ammo indicator, and propulsion effects based on the game state.
  /// </summary>
  public unsafe class AsteroidsShipView : QuantumEntityViewComponent
  {
    /// <summary>
    /// The canvas displaying the player's information.
    /// </summary>
    public Canvas PlayerNameCanvas;
    
    /// <summary>
    /// The 3D model of the ship.
    /// </summary>
    public GameObject Model;
    
    /// <summary>
    /// The text element displaying the player's name.
    /// </summary>
    public Text PlayerNameText;
    
    /// <summary>
    /// The particle system for the ship's propulsion effects.
    /// </summary>
    public ParticleSystem PropulsionFX;
    
    /// <summary>
    /// The UI image representing the ammo indicator.
    /// </summary>
    public UnityEngine.UI.Image AmmoIndicator; 

    /// <summary>
    /// The initial rotation of the player name canvas.
    /// </summary>
    private Quaternion _initialRotation;

    /// <summary>
    /// Called when the view is activated. 
    /// <para>Initializes the player name and rotation.</para>
    /// <para>Applies different transforms to the initial ship based on QUANTUM_YX.</para>
    /// </summary>
    /// <param name="frame">The current frame at the time of activation.</param>
    public override void OnActivate(Frame frame) 
    {
#if QUANTUM_XY
      _initialRotation = Quaternion.identity;
      Model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
#else
      _initialRotation = Quaternion.Euler(90f, 0f, 0f);
      Model.transform.localRotation = Quaternion.identity;
#endif

      if (PlayerNameText != null)
      {
        AsteroidsPlayerLink playerLink = PredictedFrame.Get<AsteroidsPlayerLink>(_entityView.EntityRef);
        RuntimePlayer playerData = PredictedFrame.GetPlayerData(playerLink.PlayerRef);
        PlayerNameText.text = playerData.PlayerNickname;
      }
    }

    /// <summary>
    /// Called on each frame update to refresh the ship's view, including player name rotation,
    /// ship model visibility, ammo indicator, and propulsion effects.
    /// </summary>
    public override void OnUpdateView()
    {
      PlayerNameCanvas.transform.rotation = _initialRotation;
      ChangeShipView(PredictedFrame.Has<AsteroidsShipRespawn>(EntityRef) == false);

      AsteroidsGameConfig config = PredictedFrame.FindAsset<AsteroidsGameConfig>(PredictedFrame.RuntimeConfig.GameConfig);
      AsteroidsShip shipFields = PredictedFrame.Get<AsteroidsShip>(EntityRef);
      AmmoIndicator.fillAmount = shipFields.AmmoCount.AsFloat / config.MaxAmmo.AsFloat;
      
      AsteroidsPlayerLink playerLink = PredictedFrame.Get<AsteroidsPlayerLink>(_entityView.EntityRef);
      Quantum.Input* input = PredictedFrame.GetPlayerInput(playerLink.PlayerRef);
      if (input->Up)
      {
        if (PropulsionFX.isPlaying == false)
        {
          PropulsionFX.Play();
        }
      }
      else
      {
        PropulsionFX.Stop();
      }
    }

    /// <summary>
    /// Changes the visibility of the ship model and player name canvas based on the ship's state.
    /// </summary>
    /// <param name="isAlive">Indicates whether the ship is alive or not.</param>
    private void ChangeShipView(bool isAlive)
    {
      Model.SetActive(isAlive);
      PlayerNameCanvas.gameObject.SetActive(isAlive);
    }
  }
}