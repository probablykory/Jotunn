﻿using System.Collections.Generic;
using Jotunn.Entities;

namespace Jotunn.Configs
{
    /// <summary>
    ///     Used to add new ItemConversions to the Fermenter
    /// </summary>
    public class FermenterConversionConfig : ConversionConfig
    {
        /// <summary>
        ///     The amount of items one conversion yields.
        /// </summary>
        public int ProducedItems { get; set; } = 4;

        /// <summary>
        ///     Turns the FermenterConversionConfig into a Valheim Fermenter.ItemConversion item.
        /// </summary>
        /// <returns>The Valheim ItemConversion</returns>
        public Fermenter.ItemConversion GetItemConversion()
        {
            Fermenter.ItemConversion conv = new Fermenter.ItemConversion()
            {
                m_producedItems = ProducedItems,
                m_from = Mock<ItemDrop>.Create(FromItem),
                m_to = Mock<ItemDrop>.Create(ToItem),
            };

            return conv;
        }

        /// <summary>
        ///     Loads a single FermenterConversionConfig from a JSON string
        /// </summary>
        /// <param name="json">JSON text</param>
        /// <returns>Loaded FermenterConversionConfig</returns>
        public static FermenterConversionConfig FromJson(string json)
        {
            return SimpleJson.SimpleJson.DeserializeObject<FermenterConversionConfig>(json);
        }

        /// <summary>
        ///     Loads a list of FermenterConversionConfigs from a JSON string
        /// </summary>
        /// <param name="json">JSON text</param>
        /// <returns>Loaded list of FermenterConversionConfigs</returns>
        public static List<FermenterConversionConfig> ListFromJson(string json)
        {
            return SimpleJson.SimpleJson.DeserializeObject<List<FermenterConversionConfig>>(json);
        }
    }
}
