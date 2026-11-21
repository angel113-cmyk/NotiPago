-- HT-20 y HT-21: pagos de prueba.
-- Ejecutar DESPUES de 01 y 02. Se puede re-ejecutar sin problema.

-- HT-20: marca de prueba. Totales y reportes la excluyen (lo hace la app con es_prueba = false).
alter table public.pagos
    add column if not exists es_prueba boolean not null default false;

create index if not exists pagos_user_prueba_idx
    on public.pagos (user_id, es_prueba, fecha_hora desc);

-- HT-21: el admin puede borrar SOLO sus registros de prueba. Los pagos reales siguen
-- sin poder borrarse ni modificarse (no hay politicas de DELETE/UPDATE para ellos).
drop policy if exists pagos_delete_pruebas on public.pagos;

create policy pagos_delete_pruebas
    on public.pagos
    for delete
    to authenticated
    using (user_id = auth.uid() and es_prueba = true);

grant delete on public.pagos to authenticated;
-- UPDATE sigue denegado:
revoke update on public.pagos from authenticated;
