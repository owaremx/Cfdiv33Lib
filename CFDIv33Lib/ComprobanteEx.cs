using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace CFDIv33Lib
{
    public partial class Comprobante
    {
        public Comprobante()
        {
            //procesamiento de impuestos
            this.versionField = "3.3";
            Conceptos = new List<ComprobanteConcepto>();
        }

        public void GenerarXml(String rutaSalida)
        {
            EscribirXml(rutaSalida);
        }

        private void EscribirXml(String rutaSalida)
        {
            decimal total = 0;
            foreach(ComprobanteConcepto concepto in conceptosField)
            {
                total += concepto.Importe;
            }
            this.totalField = total;
            this.SubTotal = total;

            XmlSerializer serializer = new XmlSerializer(typeof(Comprobante));
            XmlTextWriter writer = new XmlTextWriter(rutaSalida, Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            writer.Indentation = 4;
            serializer.Serialize(writer, this);
            writer.Close();
        }
    }
}
