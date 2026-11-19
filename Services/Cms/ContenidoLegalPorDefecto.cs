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
            AvisoDePrivacidad => ("Aviso de privacidad", SeccionesAvisoDePrivacidad(), "- Capturar razón social, RFC, domicilio y correo para privacidad en Personalizar sitio.\n- Revisión 2026-10-29: texto ajustado a la Ley Federal de Protección de Datos Personales en Posesión de los Particulares publicada el 20-03-2025 (reformada el 14-11-2025). Contenido mínimo del art. 15 (responsable y domicilio, datos, finalidades necesarias y voluntarias, opciones para limitar, medios ARCO, cambios al aviso).\n- Verificar con un asesor los plazos ARCO (20 días para responder y 15 para hacerlo efectivo, art. 31) y si se cuentan en días hábiles.\n- Confirmar la edad mínima (sección Menores de edad) y si se usarán los datos para enviar promociones (hoy no se envían; si se empieza, agregarlo como finalidad voluntaria).\n- La foto de perfil se sube a Cloudinary; los respaldos completos de la base de datos van a Google Drive y Cloudinary."),
            TerminosYCondiciones => ("Términos y condiciones", SeccionesTerminosYCondiciones(), "- Revisión 2026-10-29: estructura tomada de lo que publican las florerías en línea de México (sustitución por valor equivalente, variación con la foto, franjas sin hora exacta, alergias, causas ajenas, dedicatorias) y de la Ley Federal de Protección al Consumidor, art. 76 Bis (domicilio, teléfonos y medios de reclamación visibles antes de comprar).\n- Confirmar: edad mínima, aviso antes de sustituir la flor principal y no incluir dedicatorias ofensivas (son políticas nuevas; el sistema no las aplica solo).\n- Confirmar si los precios incluyen IVA y si se emitirán facturas (hoy no hay facturación).\n- Decidir si se fija una jurisdicción (por ejemplo, tribunales de Hidalgo) y revisar con un asesor, incluida la NMX-COE-001-SCFI-2018 de comercio electrónico."),
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
        S("Responsable de tus datos personales",
          "{{razonSocial}}, que opera la tienda {{nombreTienda}}, con domicilio en {{direccion}}, es responsable del tratamiento de tus datos personales conforme a la Ley Federal de Protección de Datos Personales en Posesión de los Particulares.\n\nPara cualquier asunto sobre tus datos escríbenos a {{correoPrivacidad}} o llámanos al {{telefono}}."),
        S("Datos personales que tratamos",
          "- Identificación y contacto: nombre, apellidos, correo electrónico y teléfono.\n- Datos opcionales de tu perfil: sexo, fecha de nacimiento y foto de perfil.\n- Datos de entrega: calle, colonia, municipio, estado, código postal y referencias del domicilio.\n- Datos de tus compras: productos, montos, fechas, forma de pago, estado del pedido y la dedicatoria que escribas.\n- Datos técnicos: dirección IP y tipo de navegador, que nuestros servidores registran por seguridad.\n\nNo tratamos datos personales sensibles. No recibimos ni guardamos los datos de tu tarjeta: el pago en línea se hace directamente en la plataforma de Mercado Pago."),
        S("Datos de la persona que recibe el arreglo",
          "Si envías flores a otra persona, el domicilio de entrega y la dedicatoria pueden ser datos suyos. Compártelos solo si cuentas con su autorización. Los usaremos únicamente para entregar el pedido."),
        S("Finalidades necesarias",
          "Usamos tus datos para estas finalidades, que son necesarias para darte el servicio que nos pides:\n- Crear y administrar tu cuenta.\n- Recibir, preparar y entregar tus pedidos, y registrar tus pagos.\n- Comunicarnos contigo sobre tus pedidos y atender dudas, aclaraciones, quejas y garantías.\n- Cumplir obligaciones legales y atender requerimientos de autoridad.\n- Mantener la seguridad del sitio y prevenir fraudes o usos indebidos de tu cuenta."),
        S("Finalidades voluntarias",
          "Si no te opones, también usamos tus datos para:\n- Elaborar estadísticas y análisis de compra (por ejemplo, por edad, sexo, colonia o productos preferidos) para mejorar nuestro catálogo y nuestras promociones.\n\nSi no quieres que los usemos para esto, escríbenos a {{correoPrivacidad}} con el asunto «Finalidades voluntarias». Negarte no afecta tus compras ni el servicio que te damos."),
        S("Con quién compartimos tus datos",
          "- Mercado Pago, para procesar tus pagos en línea: recibe tu correo electrónico y el detalle del pedido que pagas.\n- Proveedores que nos dan servicios tecnológicos (alojamiento del sitio, base de datos, almacenamiento de imágenes, respaldos y avisos internos a nuestro personal). Tratan tus datos solo por encargo nuestro, para esos fines y con medidas de confidencialidad.\n- Autoridades competentes, cuando una ley o una orden de autoridad nos lo exija.\n\nNo vendemos, rentamos ni cedemos tus datos personales a terceros para fines comerciales."),
        S("Cómo limitar el uso de tus datos",
          "- Puedes editar tu perfil y borrar tus direcciones guardadas desde tu cuenta.\n- Puedes cerrar la sesión en todos tus dispositivos desde tu configuración.\n- Puedes pedirnos que limitemos el uso o la divulgación de tus datos escribiendo a {{correoPrivacidad}}."),
        S("Tus derechos ARCO",
          "Tienes derecho a acceder a tus datos, a rectificarlos si son inexactos, a cancelarlos cuando ya no sean necesarios y a oponerte a su uso para fines específicos. También puedes revocar tu consentimiento.\n\nPara ejercerlos, envía un correo a {{correoPrivacidad}} con el asunto «Derechos ARCO» e incluye:\n- Tu nombre completo y el correo de tu cuenta.\n- Una copia de tu identificación oficial o, si actúas en nombre de otra persona, el documento que acredite la representación.\n- El derecho que quieres ejercer y los datos a los que se refiere.\n- Cualquier documento que ayude a localizar tus datos.\n\nTe comunicaremos nuestra respuesta en un plazo máximo de 20 días desde que recibamos tu solicitud y, si procede, la haremos efectiva dentro de los 15 días siguientes. Estos plazos pueden ampliarse una sola vez por un periodo igual cuando el caso lo justifique.\n\nSi consideras que se vulneró tu derecho a la protección de tus datos, puedes acudir ante la autoridad competente, que hoy es la Secretaría Anticorrupción y Buen Gobierno."),
        S("Cookies y almacenamiento en tu navegador",
          "Este sitio no usa cookies. Para que funcione guardamos en tu navegador tu sesión, tu carrito, el borrador de tu pedido y tus preferencias, y una copia de páginas e información pública para que puedas consultarlas sin conexión. Puedes borrar estos datos cerrando sesión o desde la configuración de tu navegador.\n\nPara mostrar tipografías y el mapa de la página de Contacto, tu navegador se conecta a servicios de Google, que pueden recibir tu dirección IP."),
        S("Menores de edad",
          "Nuestra tienda está dirigida a personas mayores de edad. No recabamos de forma intencional datos de menores. Si eres madre, padre o tutor y crees que un menor nos dio sus datos, escríbenos a {{correoPrivacidad}} para eliminarlos."),
        S("Consentimiento",
          "Al registrarte o hacer un pedido después de conocer este aviso, aceptas que tratemos tus datos para las finalidades necesarias. Puedes oponerte a las finalidades voluntarias en cualquier momento, como se indica arriba."),
        S("Cambios a este aviso",
          "Podemos modificar este aviso por cambios en la ley, en nuestros servicios o en nuestras prácticas. Publicaremos la versión vigente en esta página con su fecha de actualización. Si un cambio implica usar tus datos para una finalidad nueva, te pediremos otra vez tu consentimiento."),
    ];

    private static List<SeccionLegalDto> SeccionesTerminosYCondiciones() =>
    [
        S("Quiénes somos",
          "{{razonSocial}} opera la tienda en línea {{nombreTienda}}, una florería con domicilio en {{direccion}}. Para aclaraciones o reclamaciones puedes llamarnos al {{telefono}} o escribirnos a {{correo}}."),
        S("Aceptación de estos términos",
          "Estos términos regulan el uso del sitio y las compras que hagas en él. Al crear una cuenta o hacer un pedido aceptas estos términos, el Aviso de privacidad y nuestras políticas de Envíos y entregas y de Cancelaciones y reembolsos. Si no estás de acuerdo, te pedimos no usar la tienda en línea."),
        S("Quién puede comprar",
          "- Para comprar debes ser mayor de edad o contar con la supervisión de tu madre, padre o tutor.\n- Necesitas una cuenta con datos verdaderos y actualizados.\n- Eres responsable de cuidar tu contraseña y de lo que se haga con tu cuenta. Si notas un uso que no reconoces, cambia tu contraseña, cierra la sesión en todos tus dispositivos desde tu configuración y avísanos."),
        S("Arreglos, fotografías y sustituciones",
          "- Cada arreglo se hace a mano, por lo que puede tener pequeñas diferencias con la fotografía.\n- Las flores son de temporada: si alguna no está disponible, la sustituiremos por otra de calidad y valor iguales o mayores, cuidando el estilo, los colores y el tamaño del arreglo.\n- Si la flor principal del arreglo no está disponible, te contactaremos antes de hacer el cambio."),
        S("Precios y disponibilidad",
          "- Los precios están en pesos mexicanos.\n- El costo de envío se calcula según el código postal de entrega y se muestra antes de pagar.\n- El precio que pagas es el vigente al momento de confirmar tu pedido.\n- Los productos están sujetos a disponibilidad para la fecha de entrega. Si no hay disponibilidad para la fecha que elijas, el sistema te lo indicará y te sugerirá otra fecha."),
        S("Ofertas, descuentos y cupones",
          "- Las ofertas aplican a los productos y en las fechas que se indican, y son válidas solo durante su vigencia.\n- Los descuentos automáticos y los cupones no se acumulan: se aplica el que te dé el mayor ahorro.\n- Los cupones pueden tener vigencia, monto mínimo de compra y límite de usos."),
        S("Pedidos",
          "- Puedes hacer un pedido inmediato, para entrega el mismo día, o programarlo para una fecha y una franja de horario. No podemos garantizar una hora exacta de entrega.\n- Cada pedido pasa por estos estados: pendiente de validación, en preparación, en ruta y entregado. Puedes consultarlo en «Mis pedidos».\n- Podemos rechazar o cancelar un pedido si no es posible surtirlo o entregarlo, si los datos de entrega son incorrectos o si detectamos un posible fraude. En ese caso te avisaremos y te devolveremos lo que hayas pagado."),
        S("Pagos",
          "- En la tienda en línea el pago se hace con Mercado Pago, con los medios que este ofrezca (tarjeta y, en su caso, pago en efectivo o transferencia).\n- No recibimos ni guardamos los datos de tu tarjeta.\n- Si eliges un medio que no se acredita al momento, como efectivo o transferencia, págalo cuanto antes: mientras el pago no se acredite, el pedido queda con saldo pendiente.\n- Si tu pago no se completa, puedes reintentarlo.\n- En nuestra sucursal aceptamos efectivo, tarjeta y transferencia."),
        S("Entregas",
          "- Las zonas, costos y horarios de entrega están en la política de Envíos y entregas.\n- Verifica que la dirección y las referencias sean correctas: no somos responsables de entregas fallidas o retrasos causados por datos incompletos o equivocados.\n- Si la persona que recibe tiene alergias, revisa la composición del arreglo antes de comprar."),
        S("Dedicatorias",
          "La dedicatoria es opcional, de hasta 250 caracteres, y su contenido es responsabilidad de quien la escribe. Podemos no incluir mensajes ofensivos, discriminatorios o ilícitos."),
        S("Cancelaciones, reembolsos y reclamaciones",
          "Cómo cancelar un pedido, cuándo procede un reembolso y cómo reclamar si tu arreglo llegó dañado se explica en la política de Cancelaciones y reembolsos."),
        S("Causas ajenas a nosotros",
          "No somos responsables de retrasos o incumplimientos causados por situaciones fuera de nuestro control, como fenómenos naturales, condiciones del clima, bloqueos de caminos, fallas generales de servicios o disposiciones de autoridad. Si ocurre, te avisaremos y acordaremos contigo una nueva fecha o el reembolso."),
        S("Uso del sitio y propiedad intelectual",
          "- No está permitido usar el sitio para fines ilícitos, intentar acceder a cuentas o áreas que no te corresponden, ni afectar su funcionamiento.\n- Los textos, fotografías, logotipos y diseños del sitio pertenecen a {{nombreTienda}} o se usan con autorización, y no pueden copiarse sin permiso."),
        S("Datos personales",
          "La forma en que tratamos tus datos personales se explica en el Aviso de privacidad."),
        S("Cambios a estos términos",
          "Podemos actualizar estos términos. La versión vigente es la publicada en esta página, con su fecha de actualización. Los pedidos ya hechos se rigen por los términos vigentes al momento de hacerlos."),
        S("Legislación y quejas",
          "- Estos términos se rigen por las leyes de los Estados Unidos Mexicanos, en particular por la Ley Federal de Protección al Consumidor.\n- Si tienes una queja, escríbenos primero a {{correo}} o llámanos al {{telefono}} para resolverla.\n- Como consumidor, también puedes acudir a la Procuraduría Federal del Consumidor (PROFECO)."),
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
