﻿using UnityEngine;
using System.Linq;

using AGXUnity.BrickUnity.Signals;

using B_Component = Brick.Physics.Component;
using B_RigidBody = Brick.Physics.Mechanics.RigidBody;
using B_Connector = Brick.Physics.Mechanics.AttachmentPairConnector;
using B_Signal = Brick.Signal;
using B_BrickSimulation = Brick.AGXBrick.BrickSimulation;
using B_Interaction = Brick.Physics.Mechanics.AttachmentPairInteraction;
using B_Agent = Brick.MachineLearning.ReinforcementLearning.AgentSettings;

namespace AGXUnity.BrickUnity
{
  public class BrickRuntimeComponent : AGXUnity.ScriptComponent
  {
    public string filePath;
    public string modelName;

    protected B_Component m_component;
    private B_BrickSimulation m_brickSimulation;

    public B_Component Component => m_component;
    public B_BrickSimulation BrickSimulation => m_brickSimulation;

    public B_Agent GetBrickAgent(string agentName)
    {
      B_Agent agent = null;

      var b_agents = m_component._RecursiveValues.OfType<B_Agent>();
      foreach (var b_agent in b_agents)
      {
        var name = b_agent._ModelValuePath.Name.Str;
        if (name.Equals(agentName))
        {
          agent = b_agent;
        }
      }
      if (agent == null)
        Debug.LogWarning("Could not find brick agent with name: " + agentName);

      return agent;
    }


    public void Reload()
    {
      Brick.Model.MarkDirtyModels();
      var b_component = BrickUtils.LoadComponentFromFile(filePath, modelName);
      ReloadBodies(b_component);
    }

    public void ReloadBodies(B_Component b_component)
    {
      foreach (var au_body in GetComponentsInChildren<AGXUnity.RigidBody>())
      {
        var c_brickObject = au_body.GetComponent<BrickObject>();
      }
    }


    protected override bool Initialize()
    {
      BrickUtils.SetupBrickEnvironment();
      Debug.Log($"Synchronizing Brick component {filePath}:{modelName}");
      Brick.Model.MarkDirtyModels();
      m_component = BrickUtils.LoadComponentFromFile(filePath, modelName);
      var au_sim = AGXUnity.Simulation.Instance.GetInitialized<AGXUnity.Simulation>();
      m_brickSimulation = new B_BrickSimulation(au_sim.Native);
      au_sim.StepCallbacks.PreStepForward += m_brickSimulation.SyncInputParameters;
      au_sim.StepCallbacks.PostStepForward += m_brickSimulation.SyncOutputParameters;

      // TODO: Temporary fix to make sure the external component parameters from AGX are
      // transfered to the Brick object
      var b_tempSim = new B_BrickSimulation();
      b_tempSim.AddComponent(m_component);

      HandleRigidBodies();
      HandleGeometries();
      HandleConstraints();
      AddSignals();
      AddROSConnection();
      AddDriveTrains();
      AddSuctionCup();
      if (Application.isEditor)
        HandleSignals();
      m_brickSimulation.ConnectToROS();

      // Just to be on the safe side
      b_tempSim.ClearAgxSimulation();
      return base.Initialize();
    }


    private void HandleRigidBodies()
    {
      foreach (var au_body in GetComponentsInChildren<AGXUnity.RigidBody>())
      {
        var c_brickObject = au_body.GetComponent<BrickObject>();
        if (c_brickObject == null || !c_brickObject.synchronize)
          continue;
        var b_body = c_brickObject.GetBrickValue<B_RigidBody>(m_component);
        if (b_body == null)
          continue;
        var agx_body = au_body.GetInitialized<AGXUnity.RigidBody>().Native;
        this.m_brickSimulation.BodyMap.Add(b_body, agx_body);
      }
    }


    private void HandleGeometries()
    {
      foreach (var au_shape in GetComponentsInChildren<AGXUnity.Collide.Shape>())
      {
        var c_brickObject = au_shape.GetComponent<BrickObject>();
        if (c_brickObject == null || !c_brickObject.synchronize)
          continue;
        var b_geometry = c_brickObject.GetBrickValue<Brick.Physics.Geometry>(m_component);
        if (b_geometry == null)
          continue;
        var agx_geom = au_shape.GetInitialized<AGXUnity.Collide.Shape>().NativeGeometry;
        this.m_brickSimulation.GeometryMap.Add(b_geometry, agx_geom);
      }
    }


    private void HandleConstraints()
    {
      foreach (var au_constraint in GetComponentsInChildren<AGXUnity.Constraint>())
      {
        var c_brickObject = au_constraint.GetComponent<BrickObject>();
        if (c_brickObject == null || !c_brickObject.synchronize)
          continue;
        var b_connector = c_brickObject.GetBrickValue<B_Connector>(m_component);
        if (b_connector == null)
          continue;
        var nativeConstraint = au_constraint.GetInitialized<AGXUnity.Constraint>().Native;
        this.m_brickSimulation.InteractionMap.Add(b_connector.MainInteraction, nativeConstraint);
        m_brickSimulation.ConnectorMap.Add(b_connector, nativeConstraint);
        foreach (var b_interaction in b_connector.Interactions)
        {
          if (b_interaction is B_Interaction.Interaction1D b_interaction1D)
          {
            this.m_brickSimulation.InteractionMap.Add(b_interaction1D, nativeConstraint);
          }
          else if (b_interaction != b_connector.MainInteraction && !(b_interaction is B_Interaction.Gear1DInteraction))
          {
            var brickObject = GetComponent<BrickObject>();
            Debug.LogError($"Unhandled Brick Interaction: {brickObject.path}. The Interaction is neither a MainInteraction or a controller.");
          }
        }
      }
    }


    private void AddDriveTrains()
    {
      m_brickSimulation.CreatePowerLineUnits(m_component);
      m_brickSimulation.ConnectPowerLineUnits(m_component);
    }


    private void AddROSConnection()
    {
      this.m_brickSimulation.CreateROSConnection(m_component);
    }


    private void AddSignals()
    {
      this.m_brickSimulation.CreateSignals(m_component);
      AddCameraSignals();
    }


    private void AddSuctionCup()
    {
      m_brickSimulation.CreateSuctionGrippers(m_component);

      foreach (var suctionGripper in GetComponentsInChildren<SuctionGripper>())
      {
        var c_brickObject = suctionGripper.GetComponent<BrickObject>();
        if (c_brickObject == null || !c_brickObject.synchronize)
          continue;
        var b_suctionGripper = c_brickObject.GetBrickValue<Brick.AGXBrick.SuctionGripper>(m_component);
        if (b_suctionGripper == null)
          continue;

        var agx_suctionGripper = m_brickSimulation.GetAgxSuctionGripper(b_suctionGripper);
        suctionGripper.InitSuctionGripper(b_suctionGripper, agx_suctionGripper);
      }
    }


    // Add the camera signal components to the cameras that are used in signals
    private void AddCameraSignals()
    {
      var b_outputs = m_brickSimulation.OutputSignals;

      foreach (var b_output in b_outputs)
      {
        if ( b_output is B_Signal.CameraOutput)
        {
          B_Signal.CameraOutput cameraSignal = (B_Signal.CameraOutput)b_output;
          var camera = transform.Find(cameraSignal.Camera._ModelValuePath.Name.Str);
          var cameraOutput = camera.gameObject.AddComponent<BrickCameraOutput>();
          cameraOutput.b_cameraOutput = cameraSignal;
        }
      }
    }


    // Create GameObjects for the Signals. These GameObjects can be used to monitor signals and set them if they are
    // inputs. If no Signals exist in m_brickSimulation then no GameObject will be created.
    private void HandleSignals()
    {
      var go_inputs = HandleInputSignals();
      var go_outputs = HandleOutputSignals();

      if (go_inputs is null && go_outputs is null)
        return;

      var go_signals = new GameObject("Signals");
      go_signals.transform.SetParent(this.transform);

      if (go_inputs != null)
        go_inputs.transform.SetParent(go_signals.transform);

      if (go_outputs != null)
        go_outputs.transform.SetParent(go_signals.transform);
    }


    // Create GameObjects for input Signals. They will all share a common parent GameObject. Returns null if no inputs
    // exist in m_brickSimulation.
    private GameObject HandleInputSignals()
    {
      var b_inputs = m_brickSimulation.InputSignals;
      if (b_inputs.Count < 1)
        return null;

      var go_inputs = new GameObject("Inputs");
      foreach (var b_input in b_inputs)
      {
        var go = new GameObject(b_input.GetValueNameOrModelPath());
        go.transform.SetParent(go_inputs.transform);
        switch (b_input)
        {
          case B_Signal.Input<double> doubleInput:
            {
              var comp = go.AddComponent<BrickDoubleInput>();
              comp.signal = doubleInput;
            }
            break;
          case B_Signal.Input<long> intInput:
            {
              var comp = go.AddComponent<BrickIntInput>();
              comp.signal = intInput;
            }
            break;
          case B_Signal.Input<bool> boolInut:
            {
              var comp = go.AddComponent<BrickBoolInput>();
            }
            break;
          default:
            Debug.LogWarning($"Unkown input signal type: {b_input.GetType()}");
            break;
        }
      }
      return go_inputs;
    }


    // Create GameObjects for output Signals. They will all share a common parent GameObject. Returns null if no
    // outputs exist in m_brickSimulation.
    private GameObject HandleOutputSignals()
    {
      var b_outputs = m_brickSimulation.OutputSignals;
      if (b_outputs.Count < 1)
        return null;

      var go_outputs = new GameObject("Outputs");
      foreach (var b_output in b_outputs)
      {
        var go = new GameObject(b_output.GetValueNameOrModelPath());
        go.transform.SetParent(go_outputs.transform);
        switch (b_output)
        {
          case B_Signal.Output<double> doubleOutput:
            {
              var comp = go.AddComponent<BrickDoubleOutput>();
              comp.signal = doubleOutput;
            }
            break;
          case B_Signal.Output<Brick.Math.Vec3> vec3Output:
            {
              var comp = go.AddComponent<BrickVec3Output>();
              comp.signal = vec3Output;
            }
            break;
          case B_Signal.Output<Brick.Math.Quat> quatOutput:
            {
              var comp = go.AddComponent<BrickQuatOutput>();
              comp.signal = quatOutput;
            }
            break;
          case B_Signal.Output<Brick.Scene.Transform> transformOutput:
            {
              var comp = go.AddComponent<BrickTransformOutput>();
              comp.signal = transformOutput;
            }
            break;
          case B_Signal.Output<long> intOutput:
            {
              var comp = go.AddComponent<BrickIntOutput>();
              comp.signal = intOutput;
            }
            break;
          case B_Signal.CameraOutput cameraOutput:
            // No object added for camera signals right now
            break;
          default:
            Debug.LogWarning($"Unkown output signal type: {b_output.GetType()}");
            break;
        }

      }
      return go_outputs;
    }

  }
}
