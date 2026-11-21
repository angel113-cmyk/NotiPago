# HT-03: usuario admin y registro (configuracion de Auth)

Decision del dueno: la cuenta se crea **desde la app Android** ("Crear cuenta") y el registro queda **abierto**.
Nunca uses ni compartas la `service_role`.

## 1. Configurar Supabase (una sola vez)
1. **Authentication -> Sign In / Providers**:
   - Deja **Allow new users to sign up** ENCENDIDO.
   - Deja habilitado solo el proveedor **Email**. Desactiva anonymous, phone, etc.
2. En el proveedor **Email**, apaga **Confirm email** (si no, la app no podra iniciar sesion
   hasta que se abra un correo de confirmacion).
3. Guarda.

## 2. Crear la cuenta
- Desde la app Android (pantalla de login -> "Crear cuenta"), o
- a mano en **Authentication -> Users -> Add user -> Create new user** (marca *Auto Confirm User*).

## 3. Reglas importantes
- **La PC y el celular deben usar la MISMA cuenta.** Con RLS cada cuenta ve solo sus propios pagos:
  si creas dos cuentas, los pagos del celular no aparecen en la PC.
- Como el registro esta abierto, cualquiera con el APK podria crear cuentas (no veria tus datos, pero
  habria cuentas basura). Si mas adelante quieres cerrarlo, apaga *Allow new users to sign up*.

## 4. Claves que usan las apps
*Project Settings -> API Keys*: solo la **publishable (anon)** va en `appsettings.json`.
La contrasena de la base de datos y la `secret/service_role` no van en ninguna app.
