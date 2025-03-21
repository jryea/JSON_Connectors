using System;
using System.Collections.Generic;
using System.Text;

namespace Core.Models.Elements
{
    /// <summary>
    /// Container for all structural elements in the model
    /// </summary>
    public class ElementContainer
    {
        /// <summary>
        /// Collection of floor elements
        /// </summary>
        public List<Floor> Floors { get; set; } = new List<Floor>();

        /// <summary>
        /// Collection of wall elements
        /// </summary>
        public List<Wall> Walls { get; set; } = new List<Wall>();

        /// <summary>
        /// Collection of beam elements
        /// </summary>
        public List<Beam> Beams { get; set; } = new List<Beam>();

        /// <summary>
        /// Collection of brace elements
        /// </summary>
        public List<Brace> Braces { get; set; } = new List<Brace>();

        /// <summary>
        /// Collection of column elements
        /// </summary>
        public List<Column> Columns { get; set; } = new List<Column>();

        /// <summary>
        /// Collection of isolated footing elements
        /// </summary>
        public List<IsolatedFooting> IsolatedFootings { get; set; } = new List<IsolatedFooting>();

        /// <summary>
        /// Collection of joint elements
        /// </summary>
        public List<Joint> Joints { get; set; } = new List<Joint>();

        /// <summary>
        /// Collection of continuous footing elements
        /// </summary>
        public List<ContinuousFooting> ContinuousFootings { get; set; } = new List<ContinuousFooting>();

        /// <summary>
        /// Collection of pile elements
        /// </summary>
        public List<Pile> Piles { get; set; } = new List<Pile>();

        /// <summary>
        /// Collection of pier elements
        /// </summary>
        public List<Pier> Piers { get; set; } = new List<Pier>();

        /// <summary>
        /// Collection of drilled pier elements
        /// </summary>
        public List<DrilledPier> DrilledPiers { get; set; } = new List<DrilledPier>();
    }
}