﻿namespace OmniBlade
open Prime
open Nu

[<RequireQualifiedAccess>]
module Simulants =

    // global
    let Game = Default.Game
    let Splash = Screen "Splash"

    // title
    let Title = Screen "Title"
    let TitleGui = Title / "Gui"
    let TitlePlay = TitleGui / "Play"
    let TitleCredits = TitleGui / "Credits"
    let TitleExit = TitleGui / "Exit"

    // credits
    let Credits = Screen "Credits"
    let CreditsGui = Credits / "Gui"
    let CreditsBack = CreditsGui / "Back"

    // battle
    let Battle = Default.Screen
    let BattleHud = Battle / "Hud"
    let BattleBack = BattleHud / "Back"
    let Scene = Battle / "Scene"
    let Ally (index : int) = Scene / ("Ally" + "+" + scstring index)
    let Input (index : int) = Battle / ("Input" + "+" + scstring index)
    let RegularMenu (index : int) = Input index / "RegularMenu"
    let SpecialMenu (index : int) = Input index / "SpecialMenu"
    let ItemMenu (index : int) = Input index / "ItemMenu"
    let Reticles (index : int) = Input index / "Reticles"
    let AllInputEntities (index : int) = [RegularMenu index; SpecialMenu index; ItemMenu index; Reticles index]