import { useMsal } from '@azure/msal-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

export function Login() {
  const { instance } = useMsal()

  return (
    <div className="mx-auto mt-16 w-full max-w-sm">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Sign in to NomadRules</CardTitle>
        </CardHeader>
        <CardContent>
          {/* Entra's hosted flow covers both sign-in and sign-up (new accounts
              use the "No account? Sign up" link on that page); renewal dates
              are set afterward on the Profile page. */}
          <Button className="w-full" onClick={() => instance.loginRedirect()}>
            Continue
          </Button>
        </CardContent>
      </Card>
    </div>
  )
}
