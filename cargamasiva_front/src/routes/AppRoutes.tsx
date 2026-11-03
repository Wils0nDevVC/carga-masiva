import { Navigate, Route, Routes } from 'react-router'
import Layout from '../components/Layout'
import DetallePage from '../pages/DetallePage'
import HistorialPage from '../pages/HistorialPage'
import LoginPage from '../pages/LoginPage'
import NoEncontradaPage from '../pages/NoEncontradaPage'
import SubirPage from '../pages/SubirPage'
import RutaProtegida from './RutaProtegida'

// Las rutas de la SPA no empiezan por /auth ni /cargas: esos prefijos se reenvían al Gateway (mismo origen).
export default function AppRoutes() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<Navigate to="/historial" replace />} />
        <Route path="/login" element={<LoginPage />} />
        <Route element={<RutaProtegida />}>
          <Route path="/subir" element={<SubirPage />} />
          <Route path="/historial" element={<HistorialPage />} />
          <Route path="/historial/:idCarga" element={<DetallePage />} />
        </Route>
        <Route path="*" element={<NoEncontradaPage />} />
      </Route>
    </Routes>
  )
}
