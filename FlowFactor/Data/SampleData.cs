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
        new Recipe
        {
            Id = "iron-plate",
            OutputItemId = "iron-plate",
            OutputAmount = 1,
            TimeSeconds = 3.2,
            MachineType = "furnace",
            Inputs = new Dictionary<string, double>
            {
                ["iron-ore"] = 1
            }
        },
        new Recipe
        {
            Id = "copper-plate",
            OutputItemId = "copper-plate",
            OutputAmount = 1,
            TimeSeconds = 3.2,
            MachineType = "furnace",
            Inputs = new Dictionary<string, double>
            {
                ["copper-ore"] = 1
            }
        },
        new Recipe
        {
            Id = "steel-plate",
            OutputItemId = "steel-plate",
            OutputAmount = 1,
            TimeSeconds = 16.0,
            MachineType = "furnace",
            Inputs = new Dictionary<string, double>
            {
                ["iron-plate"] = 5
            }
        },

        new Recipe
        {
            Id = "iron-ore-mining",
            OutputItemId = "iron-ore",
            OutputAmount = 1,
            TimeSeconds = 2.0,
            MachineType = "drill",
            Inputs = new Dictionary<string, double>()
        },
        new Recipe
        {
            Id = "copper-ore-mining",
            OutputItemId = "copper-ore",
            OutputAmount = 1,
            TimeSeconds = 2.0,
            MachineType = "drill",
            Inputs = new Dictionary<string, double>()
        },
        new Recipe
        {
            Id = "coal-mining",
            OutputItemId = "coal",
            OutputAmount = 1,
            TimeSeconds = 2.0,
            MachineType = "drill",
            Inputs = new Dictionary<string, double>()
        },
        new Recipe
        {
            Id = "stone-mining",
            OutputItemId = "stone",
            OutputAmount = 1,
            TimeSeconds = 2.0,
            MachineType = "drill",
            Inputs = new Dictionary<string, double>()
        },

        new Recipe
        {
            Id = "iron-gear-wheel",
            OutputItemId = "iron-gear-wheel",
            OutputAmount = 1,
            TimeSeconds = 0.5,
            MachineType = "assembler",
            Inputs = new Dictionary<string, double>
            {
                ["iron-plate"] = 2
            }
        },
        new Recipe
        {
            Id = "copper-cable",
            OutputItemId = "copper-cable",
            OutputAmount = 2,
            TimeSeconds = 0.5,
            MachineType = "assembler",
            Inputs = new Dictionary<string, double>
            {
                ["copper-plate"] = 1
            }
        },
        new Recipe
        {
            Id = "green-circuit",
            OutputItemId = "green-circuit",
            OutputAmount = 1,
            TimeSeconds = 0.5,
            MachineType = "assembler",
            Inputs = new Dictionary<string, double>
            {
                ["iron-plate"] = 1,
                ["copper-cable"] = 3
            }
        }
    };


    public static IReadOnlyList<MachineSpec> Machines { get; } = new List<MachineSpec>
    {
        new MachineSpec
        {
            Id = "burner-drill",
            MachineCategory = "drill",
            Name = "Твердотопливный бур",
            CraftingSpeed = 0.25,
            PowerKw = 0,

            UsesFuel = false,
            FuelItemId = null,
            FuelValueMj = 0
        },
        new MachineSpec
        {
            Id = "electric-drill",
            MachineCategory = "drill",
            Name = "Электрический бур",
            CraftingSpeed = 0.5,
            PowerKw = 90,

            UsesFuel = false,
            FuelItemId = null,
            FuelValueMj = 0
        },

        new MachineSpec
        {
            Id = "assembler1",
            MachineCategory = "assembler",
            Name = "Сборочный автомат 1",
            CraftingSpeed = 0.5,
            PowerKw = 75,

            UsesFuel = false,
            FuelItemId = null,
            FuelValueMj = 0
        },
        new MachineSpec
        {
            Id = "assembler2",
            MachineCategory = "assembler",
            Name = "Сборочный автомат 2",
            CraftingSpeed = 0.75,
            PowerKw = 90,

            UsesFuel = false,
            FuelItemId = null,
            FuelValueMj = 0
        },
        new MachineSpec
        {
            Id = "assembler3",
            MachineCategory = "assembler",
            Name = "Сборочный автомат 3",
            CraftingSpeed = 1.25,
            PowerKw = 120,

            UsesFuel = false,
            FuelItemId = null,
            FuelValueMj = 0
        },

        new MachineSpec
        {
            Id = "stone-furnace",
            MachineCategory = "furnace",
            Name = "Каменная печь",
            CraftingSpeed = 1.0,

            PowerKw = 90,          
            UsesFuel = true,
            FuelItemId = "coal",
            FuelValueMj = 8.0
        },
        new MachineSpec
        {
            Id = "steel-furnace",
            MachineCategory = "furnace",
            Name = "Стальная печь",
            CraftingSpeed = 2.0,

            PowerKw = 90,
            UsesFuel = true,
            FuelItemId = "coal",
            FuelValueMj = 8.0
        },
        new MachineSpec
        {
            Id = "electric-furnace",
            MachineCategory = "furnace",
            Name = "Электрическая печь",
            CraftingSpeed = 2.0,

            PowerKw = 180,        
            UsesFuel = false,
            FuelItemId = null,
            FuelValueMj = 0
        }
    };
}
