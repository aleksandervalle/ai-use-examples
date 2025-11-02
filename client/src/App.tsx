import { Routes, Route, Navigate } from 'react-router-dom';
import Layout from './components/Layout';
import Example1 from './pages/Example1';
import Example2 from './pages/Example2';
import Example3 from './pages/Example3';

function App() {
  return (
    <Routes>
      <Route path="/" element={<Layout />}>
        <Route index element={<Navigate to="/example-1" replace />} />
        <Route path="example-1" element={<Example1 />} />
        <Route path="example-2" element={<Example2 />} />
        <Route path="example-3" element={<Example3 />} />
      </Route>
    </Routes>
  );
}

export default App
