-- HT-01: tabla pagos, indices y restriccion anti-duplicados.
-- Ejecutar en Supabase: SQL Editor -> New query -> pegar -> Run.

create table if not exists public.pagos (
    id               uuid primary key default gen_random_uuid(),
    user_id          uuid not null default auth.uid() references auth.users (id) on delete cascade,
    app              text,                                   -- Yape, Plin, BBVA...
    tipo             text check (tipo in ('recibido', 'enviado', 'otro')),
    remitente        text,                                   -- nombre tal como llego
    monto            numeric(12, 2) not null,
    moneda           text default 'PEN',
    fecha_hora       timestamptz not null,                   -- hora de la notificacion (UTC)
    titulo_original  text,
    texto_original   text,
    paquete          text,
    hash             text not null,                          -- evita duplicados
    created_at       timestamptz default now(),
    constraint pagos_user_hash_unique unique (user_id, hash)
);

create index if not exists pagos_user_fecha_idx
    on public.pagos (user_id, fecha_hora desc);

comment on table public.pagos is 'Pagos detectados desde notificaciones del celular de la tienda.';
