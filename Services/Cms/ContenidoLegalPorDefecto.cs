using System.Text.Json;
using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.Entities;

namespace FloreriaBautista.Services.Cms;

/// <summary>
/// Documentos legales fijos y su texto provisional. Describen lo que la tienda hace hoy
/// (plan paginas-legales-y-politicas, Anexo A); el definitivo lo confirma la florería
/// desde el panel. Las variables {{...}} se reemplazan al mostrar la página.
/// </summary>
public static class ContenidoLegalPorDefecto
{
    public const string AvisoDePrivacidad        = "aviso-de-privacidad";
    public const string TerminosYCondiciones     = "terminos-y-condiciones";
    public const string EnviosYEntregas          = "envios-y-entregas";
    public const string CancelacionesYReembolsos = "cancelaciones-y-reembolsos";
    public const string PreguntasFrecuentes      = "preguntas-frecuentes";

    /// <summary>Slugs permitidos, en el orden en que se devuelven.</summary>
    public static readonly IReadOnlyList<string> Slugs =
    [
        AvisoDePrivacidad, TerminosYCondiciones, EnviosYEntregas, CancelacionesYReembolsos, PreguntasFrecuentes
    ];

    /// <summary>Variables que se pueden usar dentro de los textos.</summary>
    public static readonly IReadOnlyList<string> Variables =
    [
        "nombreTienda", "razonSocial", "direccion", "telefono", "correo", "correoPrivacidad"
    ];

    internal static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web);

    /// <summary>Crea la entidad (sin guardar) con el texto provisional del slug.</summary>
    public static ContenidoLegal Crear(string slug)
    {
        var (titulo, secciones, notas) = slug switch
        {
            AvisoDePrivacidad => ("Aviso de privacidad", SeccionesAvisoDePrivacidad(), "- Capturar razón social, RFC, domicilio y correo para privacidad en Personalizar sitio.\n- Revisar con un asesor conforme a la ley de protección de datos personales vigente y confirmar el plazo de respuesta a solicitudes ARCO.\n- Confirmar si se usarán los datos para promociones (hoy no se envían).\n- La foto de perfil se sube a Cloudinary."),
            TerminosYCondiciones => ("Términos y condiciones", SeccionesTerminosYCondiciones(), "- Confirmar la edad mínima para comprar (hoy el sistema no la pide).\n- Confirmar si los precios incluyen IVA y si se emitirán facturas (hoy no hay facturación).\n- Revisar con un asesor conforme a la Ley Federal de Protección al Consumidor."),
            EnviosYEntregas => ("Envíos y entregas", SeccionesEnviosYEntregas(), "- Confirmar la cobertura real: el sistema calcula envío a todo el país ($90 resto de Hidalgo, $170 estados colindantes, $270 resto del país). Si no se entrega fuera de la región, ajustar este texto y las tarifas en el código.\n- Confirmar si el pedido inmediato aplica solo a la zona local.\n- Definir qué pasa si nadie recibe el pedido (reintento, costo extra o entrega a un vecino)."),
            CancelacionesYReembolsos => ("Cancelaciones y reembolsos", SeccionesCancelacionesYReembolsos(), "- Confirmar el plazo para hacer el reembolso y el plazo para reclamar (el texto dice «el mismo día de la entrega»).\n- Los reembolsos no existen en el sistema: hoy se hacen a mano en Mercado Pago o por transferencia.\n- Confirmar si una cancelación tardía tiene algún cargo (hoy el texto no lo menciona)."),
            PreguntasFrecuentes => ("Preguntas frecuentes", SeccionesPreguntasFrecuentes(), "- Revisar la respuesta sobre anticipación para fechas de alta demanda (14 de febrero, 10 de mayo, Día de Muertos).\n- Agregar o quitar preguntas según lo que más pregunten los clientes."),
            _ => throw new ArgumentOutOfRangeException(nameof(slug), slug, "Slug legal no permitido.")
        };

        return new ContenidoLegal
        {
            Slug          = slug,
            Titulo        = titulo,
            SeccionesJson = JsonSerializer.Serialize(secciones, JsonOpciones),
            NotasRevision = notas,
            EsProvisional = true,
            ActualizadoEn = DateTime.UtcNow
        };
    }

    private static SeccionLegalDto S(string titulo, string cuerpo) => new() { Titulo = titulo, Cuerpo = cuerpo };

    private static List<SeccionLegalDto> SeccionesAvisoDePrivacidad() =>
    [
        S("Quién es responsable de tus datos",
          "{{razonSocial}} («{{nombreTienda}}»), con domicilio en {{direccion}}, es responsable del uso y protección de tus datos personales. Para cualquier asunto relacionado con ellos puedes escribir a {{correoPrivacidad}} o llamar al {{telefono}}."),
        S("Datos que recabamos",
          "- Datos de identificación y contacto: nombre, apellidos, correo electrónico y teléfono.\n- Datos que puedes agregar a tu perfil, si quieres: sexo, fecha de nacimiento y foto de perfil.\n- Datos de entrega: calle, colonia, municipio, estado, código postal y referencias del domicilio.\n- Datos de tus pedidos: productos, montos, fechas, estado del pedido y la dedicatoria que escribas.\n- Datos técnicos de tu conexión: dirección IP y tipo de navegador, que nuestros servidores registran por seguridad.\n\nNo recabamos datos personales sensibles. No guardamos los datos de tu tarjeta: el pago en línea se hace en la página de Mercado Pago."),
        S("Para qué usamos tus datos",
          "Finalidades necesarias para darte el servicio:\n- Crear y administrar tu cuenta.\n- Recibir, preparar y entregar tus pedidos, y registrar tus pagos.\n- Comunicarnos contigo sobre tus pedidos y atender dudas, aclaraciones o quejas.\n- Mantener la seguridad del sitio y prevenir fraudes.\n\nFinalidades adicionales, que no son necesarias para el servicio:\n- Elaborar estadísticas y análisis de compra (por ejemplo, por edad, sexo, colonia o productos preferidos) para mejorar nuestros productos y promociones.\n\nSi no quieres que usemos tus datos para las finalidades adicionales, escríbenos a {{correoPrivacidad}}. Negarte no afecta tus compras."),
        S("Con quién compartimos tus datos",
          "- Mercado Pago, para procesar tus pagos en línea: recibe tu correo electrónico y el detalle del pedido que pagas.\n- Proveedores tecnológicos que nos dan servicio de alojamiento del sitio, base de datos, almacenamiento de imágenes, respaldos y avisos internos a nuestro personal. Solo tratan tus datos por encargo nuestro y para esos fines.\n- Autoridades competentes, cuando la ley nos lo exija.\n\nNo vendemos ni rentamos tus datos personales."),
        S("Tus derechos (ARCO) y cómo ejercerlos",
          "Puedes pedir acceso a tus datos, corregirlos, cancelarlos u oponerte a su uso, y también revocar tu consentimiento o limitar su uso. Envía tu solicitud a {{correoPrivacidad}} con:\n- Tu nombre y el correo de tu cuenta.\n- Una identificación o un medio para comprobar que eres el titular.\n- Qué derecho quieres ejercer y sobre qué datos.\n\nTe responderemos por el mismo medio dentro del plazo que marca la ley."),
        S("Almacenamiento en tu navegador",
          "Este sitio no usa cookies. Para que funcione guardamos en tu navegador tu sesión, tu carrito, el borrador de tu pedido y tus preferencias, y una copia de páginas e información pública para que puedas consultarlas sin conexión. Puedes borrar estos datos cerrando sesión o desde la configuración de tu navegador.\n\nPara mostrar tipografías y el mapa de la página de Contacto, tu navegador se conecta a servicios de Google, que pueden recibir tu dirección IP."),
        S("Cambios a este aviso",
          "Cualquier cambio a este aviso de privacidad se publicará en esta misma página, con su fecha de actualización."),
    ];

    private static List<SeccionLegalDto> SeccionesTerminosYCondiciones() =>
    [
        S("Aceptación",
          "Estos términos regulan el uso de la tienda en línea de {{nombreTienda}} y las compras que hagas en ella. Al usar el sitio o hacer un pedido aceptas estos términos, el Aviso de privacidad y nuestras políticas de Envíos y entregas y de Cancelaciones y reembolsos."),
        S("Tu cuenta",
          "- Para comprar necesitas una cuenta con datos verdaderos y actualizados.\n- Eres responsable de cuidar tu contraseña y de lo que se haga con tu cuenta.\n- Puedes cerrar la sesión en todos tus dispositivos desde tu configuración."),
        S("Productos, precios y disponibilidad",
          "- Los precios están en pesos mexicanos.\n- Las fotos son ilustrativas: las flores pueden variar ligeramente según la disponibilidad de temporada, conservando el estilo y el valor del arreglo.\n- La disponibilidad depende de las flores que tengamos para la fecha de entrega. Si no hay disponibilidad para la fecha que elijas, el sistema te lo indicará y te sugerirá otra fecha."),
        S("Ofertas, descuentos y cupones",
          "- Las ofertas aplican a los productos y en las fechas que se indican.\n- Los descuentos automáticos y los cupones no se acumulan: se aplica el que te dé el mayor ahorro.\n- Los cupones pueden tener vigencia, monto mínimo de compra y límite de usos."),
        S("Pedidos",
          "- Puedes hacer un pedido inmediato, para entrega el mismo día, o programarlo para una fecha y un horario.\n- Cada pedido pasa por estos estados: pendiente de validación, en preparación, en ruta y entregado.\n- Puedes consultar el estado de tus pedidos en «Mis pedidos».\n- La dedicatoria es opcional, de hasta 250 caracteres, y su contenido es responsabilidad de quien la escribe."),
        S("Pagos",
          "- En la tienda en línea el pago se hace con Mercado Pago, con los medios que este ofrezca (tarjeta y, en su caso, pago en efectivo o transferencia).\n- Si tu pago no se completa, tu pedido queda registrado con saldo pendiente y puedes reintentar el pago.\n- En nuestra sucursal aceptamos efectivo, tarjeta y transferencia."),
        S("Envíos, cancelaciones y reembolsos",
          "- Las condiciones de entrega y sus costos están en la política de Envíos y entregas.\n- Las cancelaciones y los reembolsos se rigen por la política de Cancelaciones y reembolsos."),
        S("Uso del sitio",
          "- No está permitido usar el sitio para fines ilícitos, intentar acceder a cuentas o áreas que no te corresponden, ni afectar su funcionamiento.\n- Los textos, fotografías, logotipos y diseños del sitio pertenecen a {{nombreTienda}} o se usan con autorización."),
        S("Cambios a estos términos",
          "Podemos actualizar estos términos. La versión vigente es la publicada en esta página, con su fecha de actualización. Los pedidos ya hechos se rigen por los términos vigentes al momento de hacerlos."),
        S("Contacto y legislación",
          "- Para dudas o quejas escríbenos a {{correo}} o llámanos al {{telefono}}.\n- Estos términos se rigen por las leyes de los Estados Unidos Mexicanos.\n- Como consumidor, también puedes acudir a la Procuraduría Federal del Consumidor (PROFECO)."),
    ];

    private static List<SeccionLegalDto> SeccionesEnviosYEntregas() =>
    [
        S("Cómo se calcula el costo de envío",
          "- El costo depende del código postal de entrega y se calcula al capturar tu dirección.\n- Las entregas dentro del municipio de San Felipe Orizatlán son gratis.\n- Si tu código postal no se reconoce, el costo se confirma después de validar la dirección."),
        S("Entrega a domicilio",
          "- Los pedidos de la tienda en línea se entregan a domicilio.\n- Captura la dirección completa, con calle, colonia, código postal y referencias, para que el repartidor la encuentre sin problema.\n- Si prefieres recoger tu arreglo en la florería, haz tu pedido directamente en la sucursal o por teléfono al {{telefono}}."),
        S("Pedido inmediato",
          "El pedido inmediato se entrega el mismo día. El tiempo estimado que verás al confirmar es de 60 a 90 minutos y puede variar según la distancia y la demanda."),
        S("Pedido programado",
          "- Elige la fecha y uno de estos horarios: 9:00 a 12:00, 12:00 a 15:00, 15:00 a 18:00 o 18:00 a 21:00.\n- Si la entrega es para el mismo día, solo se ofrecen los horarios que empiezan al menos 4 horas después.\n- Si no hay disponibilidad de flores para la fecha elegida, te sugeriremos otra fecha."),
        S("Seguimiento",
          "Puedes ver el estado de tu pedido en «Mis pedidos»: pendiente de validación, en preparación, en ruta y entregado."),
        S("Pedidos que no se pudieron completar",
          "Si un pedido no se completa antes de su fecha de entrega, se marca como «No completado». Comunícate con nosotros a {{telefono}} o {{correo}} para revisarlo."),
    ];

    private static List<SeccionLegalDto> SeccionesCancelacionesYReembolsos() =>
    [
        S("Cómo cancelar un pedido",
          "- Para cancelar un pedido comunícate con nosotros lo antes posible al {{telefono}} o a {{correo}}, indicando tu nombre y el número de pedido.\n- La cancelación no se puede hacer desde la tienda en línea."),
        S("Cuándo se puede cancelar",
          "- Un pedido se puede cancelar mientras está «Pendiente de validación» o «En preparación».\n- Cuando ya está «En ruta», la cancelación solo procede si la florería la autoriza.\n- Los pedidos entregados no se pueden cancelar."),
        S("Cancelaciones por parte de la florería",
          "Podemos cancelar un pedido si no es posible prepararlo o entregarlo (por ejemplo, por falta de flores o una dirección que no se puede localizar). En ese caso te avisaremos y te ofreceremos otra fecha o el reembolso de lo pagado."),
        S("Reembolsos",
          "- Si tu pedido se cancela y ya lo habías pagado, te devolveremos lo pagado por el mismo medio de pago o por el medio que acordemos contigo.\n- Los pagos con Mercado Pago se reembolsan a través de Mercado Pago.\n- El tiempo en que el dinero se refleja depende de tu banco o del medio de pago."),
        S("Devoluciones y reclamaciones",
          "- Las flores son un producto perecedero, por lo que no aceptamos devoluciones una vez entregado el pedido.\n- Si tu arreglo llegó dañado o muy distinto a lo que pediste, comunícate con nosotros el mismo día de la entrega y, si puedes, envíanos una foto. Revisaremos tu caso para reponer el arreglo o reembolsarte."),
    ];

    private static List<SeccionLegalDto> SeccionesPreguntasFrecuentes() =>
    [
        S("¿Cómo hago un pedido?",
          "Elige tu arreglo en el catálogo, agrégalo al carrito y sigue los pasos: dirección de entrega, tipo de pedido (inmediato o programado), dedicatoria opcional y pago con Mercado Pago. Necesitas una cuenta para comprar."),
        S("¿Cuánto cuesta el envío?",
          "Depende del código postal de entrega: es gratis en San Felipe Orizatlán y en otras zonas tiene un costo fijo. Consulta las tarifas en Envíos y entregas."),
        S("¿Qué formas de pago aceptan?",
          "En la tienda en línea pagas con Mercado Pago: tarjeta y los demás medios que Mercado Pago te ofrezca. En nuestra sucursal aceptamos efectivo, tarjeta y transferencia."),
        S("¿Con cuánta anticipación debo pedir?",
          "Puedes pedir para hoy mismo con un pedido inmediato, o programarlo para otra fecha. Si programas una entrega para hoy, los horarios disponibles empiezan al menos 4 horas después. Para fechas con mucha demanda te recomendamos pedir con varios días de anticipación."),
        S("¿Puedo agregar una dedicatoria?",
          "Sí. Al hacer tu pedido puedes escribir una dedicatoria de hasta 250 caracteres."),
        S("¿Las flores serán iguales a la foto?",
          "Cuidamos que tu arreglo se vea como en la foto, pero algunas flores o el follaje pueden variar ligeramente según la temporada, conservando el estilo y el valor del arreglo."),
        S("¿Puedo personalizar mi arreglo?",
          "Desde la tienda en línea se compran los arreglos tal como aparecen en el catálogo. Si quieres algo especial, llámanos al {{telefono}} o visítanos en la florería."),
        S("¿Puedo recoger mi pedido en la florería?",
          "Los pedidos en línea se entregan a domicilio. Si prefieres recogerlo, haz tu pedido directamente en la sucursal o por teléfono."),
        S("¿Cómo sé en qué va mi pedido?",
          "Entra a «Mis pedidos» con tu cuenta. Verás si está pendiente de validación, en preparación, en ruta o entregado."),
        S("¿Puedo cancelar mi pedido?",
          "Sí, mientras no haya salido a entrega. Comunícate con nosotros al {{telefono}}. Consulta los detalles en Cancelaciones y reembolsos."),
        S("Mi pago no se completó, ¿qué hago?",
          "Tu pedido queda registrado con saldo pendiente. Puedes reintentar el pago desde la pantalla que aparece al volver de Mercado Pago o comunicarte con nosotros."),
        S("¿Cómo cuido mis flores?",
          "- Corta los tallos unos 2 cm en diagonal.\n- Cambia el agua cada 2 días.\n- Mantén el arreglo lejos del sol directo y de fuentes de calor."),
    ];
}
