using CFDIv33Lib;
using PruebaCFDI.WsFacturador;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace PruebaCFDI
{
    public class Producto
    {
        public String Id { get; set; }
        public String Descripcion { get; set; }
        public String ClaveSAT { get; set; }
        public String UnidadMedidaSAT { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal TasaIVA { get; set; }

        public Decimal IVA
        {
            get
            {
                decimal iva = CostoUnitario - CostoUnitarioSinIVA;
                return Math.Round(iva, 2);
            }
        }

        public decimal CostoUnitarioSinIVA
        {
            get
            {
                var costoSinIVA =  CostoUnitario / (1 + TasaIVA);
                return Math.Round(costoSinIVA, 2);
            }
        }
    }

    public class DetalleVenta
    {
        public Producto Producto { get; set; }
        public decimal Cantidad { get; set; }

        public decimal IVA
        {
            get
            {
                decimal iva = TotalSinIVA * Producto.TasaIVA;
                return Math.Round(iva, 2);
            }
        }

        public decimal TotalSinIVA 
        {
            get
            {
                decimal total = Producto.CostoUnitarioSinIVA * Cantidad;
                return Math.Round( total, 2);
            }
        }

        public decimal Total
        {
            get
            {
                return Math.Round(Producto.CostoUnitario * Cantidad, 2);
            }
        }
    }

    public class Venta
    {
        public List<DetalleVenta> Detalle { get; set; }
        public Venta() {
            Detalle = new List<DetalleVenta>();
        }
    }
    
    public class Program
    {
        static void Main(string[] args)
        {
            Venta v = new Venta();
            v.Detalle.Add(new DetalleVenta() {
                Producto = new Producto()
                {
                    Id="001",
                    Descripcion = "TELA X",
                    ClaveSAT="11162100",
                    UnidadMedidaSAT = "MTR",
                    TasaIVA = (decimal)0.16,
                    CostoUnitario = 69
                },
                Cantidad = (decimal)3.5,
                
            });

            v.Detalle.Add(new DetalleVenta()
            {
                Producto = new Producto()
                {
                    Id = "002",
                    Descripcion = "TELA y",
                    ClaveSAT = "11162100",
                    UnidadMedidaSAT = "MTR",
                    TasaIVA = (decimal) 0.16,
                    CostoUnitario = (decimal)196.57
                },
                Cantidad = (decimal).5,
            });

            v.Detalle.Add(new DetalleVenta()
            {
                Producto = new Producto()
                {
                    Id = "001",
                    Descripcion = "TELA z",
                    ClaveSAT = "11162100",
                    UnidadMedidaSAT = "MTR",
                    CostoUnitario = 86,
                    TasaIVA = (decimal)0.16
                },
                Cantidad = (decimal)18,
            });

            Comprobante comprobante = new Comprobante(@"C:\Users\rlopez\Desktop\SAT 3.3\cadenaoriginal_3_3.xslt", @"C:\Users\rlopez\Desktop\SAT 3.3\xsd\cfdv33.xsd")
            {
                Fecha = DateTime.Now.ToString(),
                Moneda = c_Moneda.MXN,
                TipoDeComprobante = c_TipoDeComprobante.I,
                FormaPago = c_FormaPago.Item03, //Transferencia
                FormaPagoSpecified  =true,
                MetodoPago = "PUE",
                MetodoPagoSpecified = true,
                LugarExpedicion = "91000",
                Emisor = new ComprobanteEmisor()
                {
                    Rfc= "LAN7008173R5",
                    Nombre = "CINDEMEX SA DE CV",
                    RegimenFiscal = c_RegimenFiscal.Item601 //General de Ley Personas Morales
                    
                },
                Receptor = new ComprobanteReceptor()
                {
                    Rfc= "GOYA780416GM0",
                    Nombre="RUBEN OMAR LOPEZ CRUZ",
                    UsoCFDI = c_UsoCFDI.G03
                }
            };

            foreach(DetalleVenta dv in v.Detalle)
            {
                comprobante.Conceptos.Add(new ComprobanteConcepto()
                {
                    Cantidad = dv.Cantidad,
                    ValorUnitario = dv.Producto.CostoUnitarioSinIVA,
                    Importe = dv.TotalSinIVA,
                    ClaveUnidad = dv.Producto.UnidadMedidaSAT,
                    Descripcion = dv.Producto.Descripcion,
                    ClaveProdServ = dv.Producto.ClaveSAT,
                    Impuestos = new ComprobanteConceptoImpuestos()
                    {
                        Traslados = new ComprobanteConceptoImpuestosTraslado[] {
                            new ComprobanteConceptoImpuestosTraslado() {
                                Base = dv.TotalSinIVA,
                                Impuesto = c_Impuesto.Item002,   //IVA
                                TasaOCuota = dv.Producto.TasaIVA,
                                Importe = dv.IVA,
                                ImporteSpecified = true,
                                TasaOCuotaSpecified = true,
                                TipoFactor = c_TipoFactor.Tasa
                            }
                        }
                    }
                });
            }

            String rutaFactura = @"C:\Users\rlopez\Desktop\SAT 3.3\factura.xml";

            Console.WriteLine("Generando XML");

            var result = comprobante.GenerarXml(
                rutaFactura,
                @"C:\Users\rlopez\Desktop\SAT 3.3\CSD_Pruebas_CFDI_LAN7008173R5.cer",
                @"C:\Users\rlopez\Desktop\SAT 3.3\CSD_Pruebas_CFDI_LAN7008173R5.key",
                @"12345678a"
                );

            if (!result.Correcto)
            {
                Console.Error.WriteLine("Error al generar el XML:");
                Console.Error.WriteLine(result.Mensaje);
            }
            else
            {
                Console.WriteLine("Factura generada correctamente...");
                //timbrar
                wsTimbradoSoapClient wsTimbrado = new wsTimbradoSoapClient();

                try
                {
                    ////////////////////////////////
                    // El timbrado se hace con el PAC facturadorelectronico.com
                    ////////////////////////////////

                    String xml = File.ReadAllText(rutaFactura);
                    Console.WriteLine(xml);
                    Console.WriteLine("Timbrando...");
                    wsTimbradoSoapClient solicitud = new wsTimbradoSoapClient();
                    solicitud.Open();
                    XElement xTimbre = solicitud.obtenerTimbrado(xml, "test", "TEST");
                    solicitud.Close();
                    Console.WriteLine("Respuesta del timbrado:");
                    Console.WriteLine(xTimbre.ToString());
                    Console.WriteLine("Agregando timbre...");

                    XNamespace cfdi = "http://www.sat.gob.mx/cfd/3";

                    XDocument xDoc = XDocument.Parse(xml);

                    XElement xComplemento = xDoc.Root.Element(cfdi + "Complemento");
                    if (xComplemento == null)
                    {
                        xComplemento = new XElement(cfdi + "Complemento");
                        xDoc.Root.Add(xComplemento);
                    }
                    xComplemento.Add(xTimbre);
                    xDoc.Save(rutaFactura);
                    String xmlTimbre = xDoc.ToString();
                    Console.WriteLine(xmlTimbre);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.ToString());
                }
                
            }

            Console.ReadKey();
        }
    }
}
