using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CFDIv33Lib
{
    public partial class ComprobanteConcepto
    {
        [XmlIgnore]
        public decimal IVA {
            get {
                decimal traslados = (from i in Impuestos.Traslados where i.Impuesto == c_Impuesto.Item002 select i.Importe).Sum();
                return traslados;
            }
        }

        [XmlIgnore]
        public string DescripcionUnidadMedida { get; set; }
    }
}
