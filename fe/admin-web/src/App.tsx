import { GoogleOAuthProvider } from "@react-oauth/google";
import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./providers/AuthProvider";
import { AppRoutes } from "./routes/AppRoutes";

const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;

function App() {
  return (
    <GoogleOAuthProvider clientId={googleClientId ?? ""}>
      <BrowserRouter>
        <AuthProvider>
          <AppRoutes />
        </AuthProvider>
      </BrowserRouter>
    </GoogleOAuthProvider>
  );
}

export default App;
