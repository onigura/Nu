﻿// Nu Game Engine.
// Copyright (C) Bryan Edds, 2013-2020.

namespace Nu
open System
open System.Collections.Generic
open Prime
open Nu

[<AutoOpen; ModuleBinding>]
module WorldModuleScreen =

    /// Dynamic property getters / setters.
    let internal ScreenGetters = Dictionary<string, Screen -> World -> Property> HashIdentity.Structural
    let internal ScreenSetters = Dictionary<string, Property -> Screen -> World -> bool * World> HashIdentity.Structural

    type World with
    
        static member private screenStateFinder (screen : Screen) world =
            UMap.tryFind screen.ScreenAddress world.ScreenStates

        static member private screenStateAdder screenState (screen : Screen) world =
            let screenDirectory =
                match Address.getNames screen.ScreenAddress with
                | [|screenName|] ->
                    match UMap.tryFind screenName world.ScreenDirectory with
                    | Some layerDirectory ->
                        // NOTE: this is logically a redundant operation...
                        let layerDirectory = KeyValuePair (screen.ScreenAddress, layerDirectory.Value)
                        UMap.add screenName layerDirectory world.ScreenDirectory
                    | None ->
                        let layerDirectory = KeyValuePair (screen.ScreenAddress, UMap.makeEmpty Constants.Engine.SimulantMapConfig)
                        UMap.add screenName layerDirectory world.ScreenDirectory
                | _ -> failwith ("Invalid screen address '" + scstring screen.ScreenAddress + "'.")
            let screenStates = UMap.add screen.ScreenAddress screenState world.ScreenStates
            World.choose { world with ScreenDirectory = screenDirectory; ScreenStates = screenStates }

        static member private screenStateRemover (screen : Screen) world =
            let screenDirectory =
                match Address.getNames screen.ScreenAddress with
                | [|screenName|] -> UMap.remove screenName world.ScreenDirectory
                | _ -> failwith ("Invalid screen address '" + scstring screen.ScreenAddress + "'.")
            let screenStates = UMap.remove screen.ScreenAddress world.ScreenStates
            World.choose { world with ScreenDirectory = screenDirectory; ScreenStates = screenStates }

        static member private screenStateSetter screenState (screen : Screen) world =
#if DEBUG
            if not (UMap.containsKey screen.ScreenAddress world.ScreenStates) then
                failwith ("Cannot set the state of a non-existent screen '" + scstring screen.ScreenAddress + "'")
#endif
            let screenStates = UMap.add screen.ScreenAddress screenState world.ScreenStates
            World.choose { world with ScreenStates = screenStates }

        static member private addScreenState screenState screen world =
            World.screenStateAdder screenState screen world

        static member private removeScreenState screen world =
            World.screenStateRemover screen world

        static member private publishScreenChange (propertyName : string) (propertyValue : obj) (screen : Screen) world =

            // publish change binding
            let world =
                World.publishBindingChange propertyName screen world

            // publish change event
            let world =
                let changeData = { Name = propertyName; Value = propertyValue }
                let changeEventAddress = rtoa<ChangeData> [|"Change"; propertyName; "Event"; screen.Name|]
                let eventTrace = EventTrace.debug "World" "publishScreenChange" "" EventTrace.empty
                World.publishPlus changeData changeEventAddress eventTrace screen false world

            // fin
            world

        static member private getScreenStateOpt screen world =
             World.screenStateFinder screen world

        static member internal getScreenState screen world =
            match World.getScreenStateOpt screen world with
            | Some screenState -> screenState
            | None -> failwith ("Could not find screen with address '" + scstring screen.ScreenAddress + "'.")

        static member internal setScreenState screenState screen world =
            World.screenStateSetter screenState screen world

        static member private updateScreenStateWithoutEvent updater screen world =
            let screenState = World.getScreenState screen world
            match updater screenState with
            | Some screenState -> (true, World.setScreenState screenState screen world)
            | None -> (false, world)

        static member private updateScreenState updater propertyName propertyValue screen world =
            let (changed, world) = World.updateScreenStateWithoutEvent updater screen world
            if changed
            then World.publishScreenChange propertyName propertyValue screen world
            else world

        /// Check that a screen exists in the world.
        static member internal getScreenExists screen world =
            Option.isSome (World.getScreenStateOpt screen world)

        static member internal getScreenDispatcher screen world = (World.getScreenState screen world).Dispatcher
        static member internal getScreenModelProperty screen world = (World.getScreenState screen world).Model
        static member internal getScreenModel<'a> screen world = (World.getScreenState screen world).Model.DesignerValue :?> 'a
        static member internal getScreenEcs screen world = (World.getScreenState screen world).Ecs
        static member internal getScreenTransitionState screen world = (World.getScreenState screen world).TransitionState
        static member internal setScreenTransitionState value screen world = World.updateScreenState (fun screenState -> if value <> screenState.TransitionState then Some { screenState with TransitionState = value } else None) Property? TransitionState value screen world
        static member internal getScreenTransitionTicks screen world = (World.getScreenState screen world).TransitionTicks
        static member internal setScreenTransitionTicks value screen world = World.updateScreenState (fun screenState -> if value <> screenState.TransitionTicks then Some { screenState with TransitionTicks = value } else None) Property? TransitionTicks value screen world
        static member internal getScreenIncoming screen world = (World.getScreenState screen world).Incoming
        static member internal setScreenIncoming value screen world = World.updateScreenState (fun screenState -> Some { screenState with Incoming = value }) Property? Incoming value screen world
        static member internal getScreenOutgoing screen world = (World.getScreenState screen world).Outgoing
        static member internal setScreenOutgoing value screen world = World.updateScreenState (fun screenState -> Some { screenState with Outgoing = value }) Property? Outgoing value screen world
        static member internal getScreenPersistent screen world = (World.getScreenState screen world).Persistent
        static member internal setScreenPersistent value screen world = World.updateScreenState (fun screenState -> if value <> screenState.Persistent then Some { screenState with Persistent = value } else None) Property? Persistent value screen world
        static member internal getScreenDestroying (screen : Screen) world = List.exists ((=) (screen :> Simulant)) world.DestructionListRev
        static member internal getScreenCreationTimeStamp screen world = (World.getScreenState screen world).CreationTimeStamp
        static member internal getScreenScriptFrame screen world = (World.getScreenState screen world).ScriptFrame
        static member internal setScreenScriptFrame value screen world = World.updateScreenState (fun screenState -> if value <> screenState.ScriptFrame then Some { screenState with ScriptFrame = value } else None) Property? ScriptFrame value screen world
        static member internal getScreenName screen world = (World.getScreenState screen world).Name
        static member internal getScreenId screen world = (World.getScreenState screen world).Id

        static member internal setScreenModelProperty (value : DesignerProperty) screen world =
            World.updateScreenState
                (fun screenState ->
                    if value.DesignerValue =/= screenState.Model.DesignerValue
                    then Some { screenState with Model = { screenState.Model with DesignerValue = value.DesignerValue }}
                    else None)
                Property? Model value.DesignerValue screen world

        static member internal setScreenModel<'a> (value : 'a) screen world =
            World.updateScreenState
                (fun screenState ->
                    let valueObj = value :> obj
                    if valueObj =/= screenState.Model.DesignerValue
                    then Some { screenState with Model = { DesignerType = typeof<'a>; DesignerValue = valueObj }}
                    else None)
                Property? Model value screen world

        static member internal tryGetScreenProperty (propertyName, screen, world, property : _ outref) =
            if World.getScreenExists screen world then
                match ScreenGetters.TryGetValue propertyName with
                | (false, _) -> ScreenState.tryGetProperty (propertyName, World.getScreenState screen world, &property)
                | (true, getter) -> property <- getter screen world; true
            else false

        static member internal getScreenProperty propertyName screen world =
            match ScreenGetters.TryGetValue propertyName with
            | (false, _) ->
                let mutable property = Unchecked.defaultof<_>
                match ScreenState.tryGetProperty (propertyName, World.getScreenState screen world, &property) with
                | true -> property
                | false -> failwithf "Could not find property '%s'." propertyName
            | (true, getter) -> getter screen world

        static member internal trySetScreenProperty propertyName property screen world =
            if World.getScreenExists screen world then
                match ScreenSetters.TryGetValue propertyName with
                | (true, setter) -> setter property screen world
                | (false, _) ->
                    let mutable success = false // bit of a hack to get additional state out of the lambda
                    let world =
                        World.updateScreenState
                            (fun screenState ->
                                let mutable propertyOld = Unchecked.defaultof<_>
                                match ScreenState.tryGetProperty (propertyName, screenState, &propertyOld) with
                                | true ->
                                    if property.PropertyValue =/= propertyOld.PropertyValue then
                                        let (successInner, gameState) = ScreenState.trySetProperty propertyName property screenState
                                        success <- successInner
                                        Some gameState
                                    else None
                                | false -> None)
                            propertyName property.PropertyValue screen world
                    (success, world)
            else (false, world)

        static member internal setScreenProperty propertyName property screen world =
            if World.getScreenExists screen world then
                match ScreenSetters.TryGetValue propertyName with
                | (true, setter) ->
                    match setter property screen world with
                    | (true, world) -> world
                    | (false, _) -> failwith ("Cannot change screen property " + propertyName + ".")
                | (false, _) ->
                    World.updateScreenState
                        (fun screenState ->
                            let propertyOld = ScreenState.getProperty propertyName screenState
                            if property.PropertyValue =/= propertyOld.PropertyValue
                            then Some (ScreenState.setProperty propertyName property screenState)
                            else None)
                        propertyName property.PropertyValue screen world
            else world

        static member internal attachScreenProperty propertyName property screen world =
            if World.getScreenExists screen world then
                World.updateScreenState
                    (fun screenState -> Some (ScreenState.attachProperty propertyName property screenState))
                    propertyName property.PropertyValue screen world
            else failwith ("Cannot attach screen property '" + propertyName + "'; screen '" + screen.Name + "' is not found.")

        static member internal detachScreenProperty propertyName screen world =
            if World.getScreenExists screen world then
                World.updateScreenStateWithoutEvent
                    (fun screenState -> Some (ScreenState.detachProperty propertyName screenState))
                    screen world |>
                snd
            else failwith ("Cannot detach screen property '" + propertyName + "'; screen '" + screen.Name + "' is not found.")

        static member internal registerScreen screen world =
            let dispatcher = World.getScreenDispatcher screen world
            let world = dispatcher.Register (screen, world)
            let eventTrace = EventTrace.debug "World" "registerScreen" "" EventTrace.empty
            World.publishPlus () (rtoa<unit> [|"Register"; "Event"; screen.Name|]) eventTrace screen true world

        static member internal unregisterScreen screen world =
            let dispatcher = World.getScreenDispatcher screen world
            let eventTrace = EventTrace.debug "World" "unregisteringScreen" "" EventTrace.empty
            let world = World.publishPlus () (rtoa<unit> [|"Unregistering"; "Event"; screen.Name|]) eventTrace screen true world
            dispatcher.Unregister (screen, world)

        static member internal addScreen mayReplace screenState screen world =
            let isNew = not (World.getScreenExists screen world)
            if isNew || mayReplace then
                let world = World.addScreenState screenState screen world
                if isNew then World.registerScreen screen world else world
            else failwith ("Adding a screen that the world already contains at address '" + scstring screen.ScreenAddress + "'.")

        static member internal removeScreen3 removeLayers screen world =
            if World.getScreenExists screen world then
                let world = World.unregisterScreen screen world
                let world = removeLayers screen world
                World.removeScreenState screen world
            else world

        static member internal writeScreen4 writeLayers screen screenDescriptor world =
            let screenState = World.getScreenState screen world
            let screenDispatcherName = getTypeName screenState.Dispatcher
            let screenDescriptor = { screenDescriptor with ScreenDispatcherName = screenDispatcherName }
            let getScreenProperties = Reflection.writePropertiesFromTarget tautology3 screenDescriptor.ScreenProperties screenState
            let screenDescriptor = { screenDescriptor with ScreenProperties = getScreenProperties }
            writeLayers screen screenDescriptor world

        static member internal readScreen4 readLayers screenDescriptor nameOpt world =

            // make the dispatcher
            let dispatcherName = screenDescriptor.ScreenDispatcherName
            let dispatchers = World.getScreenDispatchers world
            let dispatcher =
                match Map.tryFind dispatcherName dispatchers with
                | Some dispatcher -> dispatcher
                | None ->
                    Log.info ("Could not find ScreenDispatcher '" + dispatcherName + "'. Did you forget to provide this dispatcher from your NuPlugin?")
                    let dispatcherName = typeof<ScreenDispatcher>.Name
                    Map.find dispatcherName dispatchers

            // make the ecs
            let ecs = world.Plugin.MakeEcs ()

            // make the screen state and populate its properties
            let screenState = ScreenState.make None dispatcher ecs
            let screenState = Reflection.attachProperties ScreenState.copy screenState.Dispatcher screenState world
            let screenState = Reflection.readPropertiesToTarget ScreenState.copy screenDescriptor.ScreenProperties screenState

            // apply the name if one is provided
            let screenState =
                match nameOpt with
                | Some name -> { screenState with Name = name }
                | None -> screenState

            // add the screen's state to the world
            let screen = Screen (ntoa screenState.Name)
            let world = World.addScreen true screenState screen world
            
            // read the screen's layers
            let world = readLayers screenDescriptor screen world |> snd
            (screen, world)

        /// View all of the properties of a screen.
        static member internal viewScreenProperties screen world =
            let state = World.getScreenState screen world
            let properties = World.getProperties state
            properties |> Array.ofList |> Array.map a_c

    /// Initialize property getters.
    let private initGetters () =
        ScreenGetters.Add ("Dispatcher", fun screen world -> { PropertyType = typeof<ScreenDispatcher>; PropertyValue = World.getScreenDispatcher screen world })
        ScreenGetters.Add ("Model", fun screen world -> let designerProperty = World.getScreenModelProperty screen world in { PropertyType = designerProperty.DesignerType; PropertyValue = designerProperty.DesignerValue })
        ScreenGetters.Add ("Ecs", fun screen world -> { PropertyType = typeof<World Ecs>; PropertyValue = World.getScreenEcs screen world })
        ScreenGetters.Add ("TransitionState", fun screen world -> { PropertyType = typeof<TransitionState>; PropertyValue = World.getScreenTransitionState screen world })
        ScreenGetters.Add ("TransitionTicks", fun screen world -> { PropertyType = typeof<int64>; PropertyValue = World.getScreenTransitionTicks screen world })
        ScreenGetters.Add ("Incoming", fun screen world -> { PropertyType = typeof<Transition>; PropertyValue = World.getScreenIncoming screen world })
        ScreenGetters.Add ("Outgoing", fun screen world -> { PropertyType = typeof<Transition>; PropertyValue = World.getScreenOutgoing screen world })
        ScreenGetters.Add ("Persistent", fun screen world -> { PropertyType = typeof<bool>; PropertyValue = World.getScreenPersistent screen world })
        ScreenGetters.Add ("ScriptFrame", fun screen world -> { PropertyType = typeof<Scripting.ProceduralFrame list>; PropertyValue = World.getScreenScriptFrame screen world })
        ScreenGetters.Add ("CreationTimeStamp", fun screen world -> { PropertyType = typeof<int64>; PropertyValue = World.getScreenCreationTimeStamp screen world })
        ScreenGetters.Add ("Name", fun screen world -> { PropertyType = typeof<string>; PropertyValue = World.getScreenName screen world })
        ScreenGetters.Add ("Id", fun screen world -> { PropertyType = typeof<Guid>; PropertyValue = World.getScreenId screen world })
        
    /// Initialize property setters.
    let private initSetters () =
        ScreenSetters.Add ("Model", fun property screen world -> if World.getScreenModel screen world =/= property.PropertyValue then (true, World.setScreenModelProperty { DesignerType = property.PropertyType; DesignerValue = property.PropertyValue } screen world) else (false, world))
        ScreenSetters.Add ("TransitionState", fun property screen world -> if World.getScreenTransitionState screen world =/= property.PropertyValue then (true, World.setScreenTransitionState (property.PropertyValue :?> TransitionState) screen world) else (false, world))
        ScreenSetters.Add ("TransitionTicks", fun property screen world -> if World.getScreenTransitionTicks screen world =/= property.PropertyValue then (true, World.setScreenTransitionTicks (property.PropertyValue :?> int64) screen world) else (false, world))
        ScreenSetters.Add ("Incoming", fun property screen world -> if World.getScreenIncoming screen world =/= property.PropertyValue then (true, World.setScreenIncoming (property.PropertyValue :?> Transition) screen world) else (false, world))
        ScreenSetters.Add ("Outgoing", fun property screen world -> if World.getScreenOutgoing screen world =/= property.PropertyValue then (true, World.setScreenOutgoing (property.PropertyValue :?> Transition) screen world) else (false, world))
        ScreenSetters.Add ("Persistent", fun property screen world -> if World.getScreenPersistent screen world =/= property.PropertyValue then (true, World.setScreenPersistent (property.PropertyValue :?> bool) screen world) else (false, world))
        
    /// Initialize getters and setters
    let internal init () =
        initGetters ()
        initSetters ()