using System.Collections.Generic;
using FlowFactor.Domain;

namespace FlowFactor.Data;

public static class SampleData
{
    public static IReadOnlyList<Item> Items { get; } = new List<Item>
    {
        new("iron-ore", "Железная руда"),
        new("copper-ore", "Медная руда"),
        new("coal", "Уголь"),
        new("stone", "Камень"),

        new("iron-plate", "Железная пластина"),
        new("copper-plate", "Медная пластина"),
        new("steel-plate", "Стальная балка"),

        new("iron-gear-wheel", "Железные шестерни"),
        new("copper-cable", "Медный провод"),
        new("green-circuit", "Электронная схема"),
    };

    public static IReadOnlyList<Recipe> Recipes { get; } = new List<Recipe>
    {
        new() { Id="iron-plate", OutputItemId="iron-plate", OutputAmount=1, TimeSeconds=3.2, MachineType="furnace",
            Inputs=new() { ["iron-ore"]=1 } },

        new() { Id="copper-plate", OutputItemId="copper-plate", OutputAmount=1, TimeSeconds=3.2, MachineType="furnace",
            Inputs=new() { ["copper-ore"]=1 } },

        new() { Id="steel-plate", OutputItemId="steel-plate", OutputAmount=1, TimeSeconds=16.0, MachineType="furnace",
            Inputs=new() { ["iron-plate"]=5 } },

        // Добыча
        new() { Id="iron-ore-mining", OutputItemId="iron-ore", OutputAmount=1, TimeSeconds=1.0, MachineType="drill", Inputs=new() },
        new() { Id="copper-ore-mining", OutputItemId="copper-ore", OutputAmount=1, TimeSeconds=1.0, MachineType="drill", Inputs=new() },
        new() { Id="coal-mining", OutputItemId="coal", OutputAmount=1, TimeSeconds=1.0, MachineType="drill", Inputs=new() },
        new() { Id="stone-mining", OutputItemId="stone", OutputAmount=1, TimeSeconds=1.0, MachineType="drill", Inputs=new() },

        new() { Id="iron-gear-wheel", OutputItemId="iron-gear-wheel", OutputAmount=1, TimeSeconds=0.5, MachineType="assembler",
            Inputs=new() { ["iron-plate"]=2 } },

        new() { Id="copper-cable", OutputItemId="copper-cable", OutputAmount=2, TimeSeconds=0.5, MachineType="assembler",
            Inputs=new() { ["copper-plate"]=1 } },

        new() { Id="green-circuit", OutputItemId="green-circuit", OutputAmount=1, TimeSeconds=0.5, MachineType="assembler",
            Inputs=new() { ["iron-plate"]=1, ["copper-cable"]=3 } },
    };

    public static IReadOnlyList<MachineSpec> Machines { get; } = new List<MachineSpec>
    {
        // Буры
        new() { Id="burner-drill", MachineCategory="drill", Name="Твердотопливный бур",
            CraftingSpeed=0.25, PowerKw=150, UsesFuel=true, FuelItemId="coal", FuelValueMj=4.0 },

        new() { Id="electric-drill", MachineCategory="drill", Name="Электрический бур",
            CraftingSpeed=0.5, PowerKw=90, UsesFuel=false },

        // Сборочные автоматы
        new() { Id="assembler1", MachineCategory="assembler", Name="Сборочный автомат 1",
            CraftingSpeed=0.5, PowerKw=75 },

        new() { Id="assembler2", MachineCategory="assembler", Name="Сборочный автомат 2",
            CraftingSpeed=0.75, PowerKw=90 },

        new() { Id="assembler3", MachineCategory="assembler", Name="Сборочный автомат 3",
            CraftingSpeed=1.25, PowerKw=120 },

        // Печки
        new() { Id="stone-furnace", MachineCategory="furnace", Name="Каменная печь",
            CraftingSpeed=1.0, PowerKw=90, UsesFuel=true, FuelItemId="coal", FuelValueMj=4.0 },

        new() { Id="steel-furnace", MachineCategory="furnace", Name="Стальная печь",
            CraftingSpeed=2.0, PowerKw=90, UsesFuel=true, FuelItemId="coal", FuelValueMj=4.0 },

        new() { Id="electric-furnace", MachineCategory="furnace", Name="Электрическая печь",
            CraftingSpeed=2.0, PowerKw=180 },

        // Энергетика
        new() { Id="boiler", MachineCategory="boiler", Name="Котёл",
            CraftingSpeed=1.0, PowerKw=1800, UsesFuel=true, FuelItemId="coal", FuelValueMj=4.0 },

        new() { Id="steam-engine", MachineCategory="generator", Name="Паровой двигатель",
            CraftingSpeed=1.0, PowerKw=-900 }
    };
}