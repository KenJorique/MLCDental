// supabase/functions/send-entry-confirmation/index.ts
import { SMTPClient } from "https://deno.land/x/denomailer/mod.ts";

Deno.serve(async (req) => {
  const { record, old_record } = await req.json();

  if (record.status !== "approved" || old_record?.status === "approved") {
    return new Response("Skip", { status: 200 });
  }

  if (!record.email || record.email.trim() === "" || record.email === "EMPTY") {
    console.log("No email on file, skipping");
    return new Response("No email, skipped", { status: 200 });
  }

  const client = new SMTPClient({
    connection: {
      hostname: "smtp.gmail.com",
      port: 465,
      tls: true,
      auth: {
        username: Deno.env.get("CLINIC_GMAIL_ADDRESS")!,
        password: Deno.env.get("CLINIC_GMAIL_APP_PASSWORD")!,
      },
    },
  });

  const apptDate = new Date(record.appointment_datetime).toLocaleString("en-PH", {
    timeZone: "Asia/Manila",
    dateStyle: "long",
    timeStyle: "short",
  });

  await client.send({
    from: `MLC Dental Clinic <${Deno.env.get("CLINIC_GMAIL_ADDRESS")}>`,
    to: record.email,
    subject: "Your appointment is confirmed",
    html: `<p>Hi ${record.patient_name},</p>
           <p>Your appointment on <b>${apptDate}</b> has been approved.</p>
           <p>— MLC Dental Clinic</p>`,
  });

  await client.close();
  return new Response("Sent", { status: 200 });
});