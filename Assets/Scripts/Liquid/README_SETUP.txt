LIQUID APP IMPROVEMENTS

FILES
- Replace LiquidEstimatorRouter.cs.
- Add PlaceholderNeuralLiquidEstimator.cs.
- Add LiquidAppController.cs.

UNITY COMPONENTS
1. Add PlaceholderNeuralLiquidEstimator to LiquidModelManager.
2. Assign:
   Classical Source = ClassicalLiquidEstimator
   Geometry = BeakerGeometry
   trained R = 0.032
   trained H = 0.120
   fill range = 0.30 to 0.80
3. In LiquidEstimatorRouter:
   Classical Estimator = ClassicalLiquidEstimator
   Neural Estimator = PlaceholderNeuralLiquidEstimator
4. Add LiquidAppController to LiquidModelManager.
5. Assign all model and UI references.

UI HIERARCHY
Canvas
  LiquidControlsPanel
    FillFractionLabel
    FillFractionSlider
    ClassicalButton
    NeuralButton
    ResetButton
    RiskLabel
    RiskBar
    ModelInformationText

RESET BUTTON
- Add LiquidAppController.ResetLiquidState to OnClick.

IMPORTANT
- The Neural placeholder is not AI.
- It exists only to test the model-switching architecture.
- Neural mode automatically applies the trained radius/height and restricts fill fraction.
